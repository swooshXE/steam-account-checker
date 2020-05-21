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
        delegate void UpdateFileTextBoxACCallback(string text);

        public static void ShowUI(bool isShown)
        {
            if (Program.mw.InvokeRequired)
            {
                SetVisibleCallback d = new SetVisibleCallback(ShowUI);
                Program.mw.loadingImage.Invoke(d, new object[] { isShown });
            }
            else
                Program.mw.loadingImage.Visible = isShown;
        }

        public static void EnableUI(bool isEnabled)
        {
            if (Program.mw.InvokeRequired)
            {
                SetEnabledCallback d = new SetEnabledCallback(EnableUI);
                Program.mw.tabControl1.Invoke(d, new object[] { isEnabled });
            }
            else
            {
                Program.mw.tabControl1.Enabled = isEnabled;
                Program.mw.ButtonStart.Enabled = isEnabled;
            }
        }

        public static void UpdateFileTextBoxAC(string text)
        {
            if (Program.mw.InvokeRequired)
            {
                UpdateFileTextBoxACCallback d = new UpdateFileTextBoxACCallback(UpdateFileTextBoxAC);
                Program.mw.textBoxFile.Invoke(d, new object[] { text });
            }
            else
                Program.mw.textBoxFile.Text = text;
        }

        public static void UpdateRemainingLabel(string text)
        {
            if (Program.mw.InvokeRequired)
            {
                UpdateRemainingLabelCallback d = new UpdateRemainingLabelCallback(UpdateRemainingLabel);
                Program.mw.remainingLabel.Invoke(d, new object[] { text });
            }
            else
                Program.mw.remainingLabel.Text = text;
        }

        public static void UpdateLoggableLabel(string text)
        {
            if (Program.mw.InvokeRequired)
            {
                UpdateLoggableLabelCallback d = new UpdateLoggableLabelCallback(UpdateLoggableLabel);
                Program.mw.loggableLabel.Invoke(d, new object[] { text });
            }
            else
                Program.mw.loggableLabel.Text = text;
        }

        public static void UpdateBadLabel(string text)
        {
            if (Program.mw.InvokeRequired)
            {
                UpdateBadLabelCallback d = new UpdateBadLabelCallback(UpdateBadLabel);
                Program.mw.badLabel.Invoke(d, new object[] { text });
            }
            else
                Program.mw.badLabel.Text = text;
        }

        public static void UpdateSGProjectedLabel(string text)
        {
            if (Program.mw.InvokeRequired)
            {
                UpdateSGProtectedLabelCallback d = new UpdateSGProtectedLabelCallback(UpdateSGProjectedLabel);
                Program.mw.steamGuardLabel.Invoke(d, new object[] { text });
            }
            else
                Program.mw.steamGuardLabel.Text = text;
        }
        public static void UpdateCheckedLabel(string text)
        {
            if (Program.mw.InvokeRequired)
            {
                UpdateCheckedLabelCallback d = new UpdateCheckedLabelCallback(UpdateCheckedLabel);
                Program.mw.checkedLabel.Invoke(d, new object[] { text });
            }
            else
                Program.mw.checkedLabel.Text = text;
        }
    }
}
