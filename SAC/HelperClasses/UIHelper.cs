namespace SAC.HelperClasses
{
    class UIHelper
    {
        delegate void SetVisibleCallback(bool isShown);
        delegate void SetEnabledCallback(bool isEnabled);
        delegate void UpdateRemainingLabelCallback(string text);
        delegate void UpdateLoggableLabelCallback(string text);
        delegate void UpdateBadLabelCallback(string text);
        delegate void UpdateSGProtectedLabelCallback(string text);
        delegate void UpdateCheckedLabelCallback(string text);
        delegate void UpdateVacLabelCallback(string text);
        delegate void UpdateFileTextBoxACCallback(string text);

        // InvokeRequired must be queried on the target control, not on Program.mw:
        // accessing mw.InvokeRequired can create the window handle on a background thread.
        private static void InvokeOn(System.Windows.Forms.Control control, System.Action action)
        {
            if (control.InvokeRequired)
                control.Invoke(new System.Windows.Forms.MethodInvoker(action));
            else
                action();
        }

        public static void ShowUI(bool isShown) =>
            InvokeOn(Program.mw.loadingImage, () => Program.mw.loadingImage.Visible = isShown);

        public static void EnableUI(bool isEnabled) =>
            InvokeOn(Program.mw.tabControl1, () =>
            {
                Program.mw.tabControl1.Enabled = isEnabled;
                Program.mw.ButtonStart.Enabled = isEnabled;
                // Stop is only meaningful while a check is running.
                Program.mw.ButtonStop.Enabled = !isEnabled;
            });

        public static void UpdateFileTextBoxAC(string text) =>
            InvokeOn(Program.mw.textBoxFile, () => Program.mw.textBoxFile.Text = text);

        public static void UpdateRemainingLabel(string text) =>
            InvokeOn(Program.mw.remainingLabel, () => Program.mw.remainingLabel.Text = text);

        public static void UpdateLoggableLabel(string text) =>
            InvokeOn(Program.mw.loggableLabel, () => Program.mw.loggableLabel.Text = text);

        public static void UpdateBadLabel(string text) =>
            InvokeOn(Program.mw.badLabel, () => Program.mw.badLabel.Text = text);

        public static void UpdateSGProjectedLabel(string text) =>
            InvokeOn(Program.mw.steamGuardLabel, () => Program.mw.steamGuardLabel.Text = text);

        public static void UpdateCheckedLabel(string text) =>
            InvokeOn(Program.mw.checkedLabel, () => Program.mw.checkedLabel.Text = text);

        public static void UpdateVacLabel(string text) =>
            InvokeOn(Program.mw.vacLabel, () => Program.mw.vacLabel.Text = text);
    }
}
