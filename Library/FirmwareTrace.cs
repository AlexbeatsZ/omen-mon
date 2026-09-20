  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Copyright © 2023-2024 Piotr Szczepański * License: GPL3
     //  https://omenmon.github.io/

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace OmenMon.Library {

    // Writes a small, durable, bounded trace around firmware-facing operations.
    // BEGIN without END in the final log identifies an operation interrupted by
    // a whole-machine freeze or forced power-off.
    public static class FirmwareTrace {

        private const long MaxBytes = 4 * 1024 * 1024;
        private static readonly object Sync = new object();
        private static readonly int ProcessId = Process.GetCurrentProcess().Id;
        private static readonly string PathCurrent = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "OmenMon-firmware.log");
        private static readonly string PathPrevious = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "OmenMon-firmware.previous.log");

        private static FileStream Stream;
        private static StreamWriter Writer;

        public static long Begin(string channel, string operation) {
            long started = Stopwatch.GetTimestamp();
            Write(channel, operation, "BEGIN", 0, String.Empty);
            return started;
        }

        public static void LockAcquired(string channel, string operation, long started) {
            Write(channel, operation, "LOCK", ElapsedMilliseconds(started), String.Empty);
        }

        public static void End(
            string channel,
            string operation,
            long started,
            Exception error = null) {

            Write(
                channel,
                operation,
                error == null ? "END" : "ERROR",
                ElapsedMilliseconds(started),
                error == null ? String.Empty : error.GetType().Name + ": " + error.Message);

        }

        private static double ElapsedMilliseconds(long started) {
            return (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        }

        private static string Escape(string value) {
            if(String.IsNullOrEmpty(value))
                return String.Empty;
            return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        private static void Open() {
            if(Writer != null)
                return;

            if(File.Exists(PathCurrent) && new FileInfo(PathCurrent).Length >= MaxBytes) {
                if(File.Exists(PathPrevious))
                    File.Delete(PathPrevious);
                File.Move(PathCurrent, PathPrevious);
            }

            Stream = new FileStream(
                PathCurrent,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete,
                4096,
                FileOptions.WriteThrough);
            Writer = new StreamWriter(Stream, new UTF8Encoding(false)) { AutoFlush = true };
        }

        private static void RotateIfNeeded() {
            if(Stream == null || Stream.Length < MaxBytes)
                return;

            Writer.Dispose();
            Writer = null;
            Stream = null;

            if(File.Exists(PathPrevious))
                File.Delete(PathPrevious);
            File.Move(PathCurrent, PathPrevious);
            Open();
        }

        private static void Write(
            string channel,
            string operation,
            string phase,
            double elapsedMilliseconds,
            string detail) {

            try {
                lock(Sync) {
                    Open();
                    RotateIfNeeded();

                    Writer.Write(DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture));
                    Writer.Write('\t');
                    Writer.Write(ProcessId.ToString(CultureInfo.InvariantCulture));
                    Writer.Write('\t');
                    Writer.Write(Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture));
                    Writer.Write('\t');
                    Writer.Write(Escape(channel));
                    Writer.Write('\t');
                    Writer.Write(Escape(operation));
                    Writer.Write('\t');
                    Writer.Write(phase);
                    Writer.Write('\t');
                    Writer.Write(elapsedMilliseconds.ToString("0.000", CultureInfo.InvariantCulture));
                    Writer.Write('\t');
                    Writer.WriteLine(Escape(detail));
                }
            } catch {
                // Diagnostics must never interrupt fan control.
            }

        }

    }

}
