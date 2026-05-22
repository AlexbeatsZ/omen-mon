  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Copyright © 2023-2024 Piotr Szczepański * License: GPL3
     //  https://omenmon.github.io/

using System;
using System.Text;
using System.Windows.Forms;

namespace OmenMon.AppGui {

    // Small GUI-facing operation log.
    public class GuiLog {

        private readonly RichTextBox Target;

        public GuiLog(RichTextBox target) {
            this.Target = target;
        }

        public void Info(string message) {
            Append("INFO", message);
        }

        public void Warn(string message) {
            Append("WARN", message);
        }

        public void Error(string message) {
            Append("ERROR", message);
        }

        public void OperationBegin(string name) {
            Append("BEGIN", name);
        }

        public void OperationResult(bool success, string details) {
            Append(success ? "OK" : "FAILED", details);
        }

        public void Clear() {
            this.Target.Clear();
        }

        public void Copy() {
            if(this.Target.TextLength > 0)
                Clipboard.SetText(this.Target.Text);
        }

        public string Text {
            get { return this.Target.Text; }
        }

        private void Append(string kind, string message) {
            if(this.Target.IsDisposed)
                return;

            if(this.Target.InvokeRequired) {
                this.Target.BeginInvoke(new Action<string, string>(Append), kind, message);
                return;
            }

            StringBuilder line = new StringBuilder();
            line.Append(DateTime.Now.ToString("HH:mm:ss"));
            line.Append(" [");
            line.Append(kind);
            line.Append("] ");
            line.Append(message);
            line.Append(Environment.NewLine);

            this.Target.AppendText(line.ToString());
            this.Target.SelectionStart = this.Target.TextLength;
            this.Target.ScrollToCaret();
        }

    }

}
