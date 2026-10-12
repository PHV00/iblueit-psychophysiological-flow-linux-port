using System;
using System.Windows.Forms;
using UnityEngine;

namespace Ibit.Core.Util
{
    /// <summary>
    /// Windows keeps the original native message boxes.
    /// On Linux, informational messages are logged. Confirmation dialogs
    /// deliberately deny/cancel until a proper Unity modal UI is implemented.
    /// This prevents the port from approving actions silently.
    /// </summary>
    public class SysMessage
    {
        private static bool IsLinux =>
            UnityEngine.Application.platform == RuntimePlatform.LinuxEditor ||
            UnityEngine.Application.platform == RuntimePlatform.LinuxPlayer;

        public static DialogResult Show(string msg, string title = "")
        {
            if (!IsLinux) return MessageBox.Show(msg, title);
            Debug.Log($"[{title}] {msg}");
            return DialogResult.OK;
        }

        public static DialogResult Warning(string msg)
        {
            if (!IsLinux) return MessageBox.Show(msg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Debug.LogWarning(msg);
            return DialogResult.OK;
        }

        public static DialogResult Error(Exception e)
        {
            if (!IsLinux) return MessageBox.Show($"{e.Message}\n\nStacktrace: {e.StackTrace}",
                e.GetType().ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
            Debug.LogError(e.ToString());
            return DialogResult.OK;
        }

        public static DialogResult Error(string msg, string title)
        {
            if (!IsLinux) return MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Debug.LogError($"[{title}] {msg}");
            return DialogResult.OK;
        }

        public static DialogResult Error(string msg)
        {
            if (!IsLinux) return MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Debug.LogError(msg);
            return DialogResult.OK;
        }

        public static DialogResult Info(string msg)
        {
            if (!IsLinux) return MessageBox.Show(msg, "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Debug.Log(msg);
            return DialogResult.OK;
        }

        public static DialogResult YesNoQuestion(string question)
        {
            if (!IsLinux) return MessageBox.Show(question, "", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            Debug.LogWarning("Confirmação indisponível no Linux; operação não confirmada: " + question);
            return DialogResult.No;
        }

        public static DialogResult OkCancelQuestion(string question)
        {
            if (!IsLinux) return MessageBox.Show(question, "", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            Debug.LogWarning("Confirmação indisponível no Linux; operação cancelada: " + question);
            return DialogResult.Cancel;
        }
    }
}
