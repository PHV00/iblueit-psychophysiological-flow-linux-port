using System;
using System.Windows.Forms;
using UnityEngine;

namespace Ibit.Core.Util
{
    public class SysMessage
    {
        private static bool IsLinux =>
    		UnityEngine.Application.platform == RuntimePlatform.LinuxEditor ||
    		UnityEngine.Application.platform == RuntimePlatform.LinuxPlayer;

        public static DialogResult Show(string msg, string title = "")
        {
            if (IsLinux)
            {
                Debug.Log($"{title}: {msg}");
                return DialogResult.OK;
            }

            return MessageBox.Show(msg, title);
        }

        public static DialogResult Warning(string msg)
        {
            if (IsLinux)
            {
                Debug.LogWarning(msg);
                return DialogResult.OK;
            }

            return MessageBox.Show(
                msg,
                "Warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        public static DialogResult Error(Exception e)
        {
            if (IsLinux)
            {
                Debug.LogException(e);
                return DialogResult.OK;
            }

            return MessageBox.Show(
                $"{e.Message}\n\nStacktrace: {e.StackTrace}",
                e.GetType().ToString(),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        public static DialogResult Error(string msg, string title)
        {
            if (IsLinux)
            {
                Debug.LogError($"{title}: {msg}");
                return DialogResult.OK;
            }

            return MessageBox.Show(
                msg,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        public static DialogResult Error(string msg)
        {
            if (IsLinux)
            {
                Debug.LogError(msg);
                return DialogResult.OK;
            }

            return MessageBox.Show(
                msg,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        public static DialogResult Info(string msg)
        {
            if (IsLinux)
            {
                Debug.Log(msg);
                return DialogResult.OK;
            }

            return MessageBox.Show(
                msg,
                "Information",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        public static DialogResult YesNoQuestion(string question)
        {
            if (IsLinux)
            {
                Debug.LogWarning(
                    $"Pergunta Yes/No não possui diálogo Linux ainda: {question}"
                );

                return DialogResult.Yes;
            }

            return MessageBox.Show(
                question,
                "",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
        }

        public static DialogResult OkCancelQuestion(string question)
        {
            if (IsLinux)
            {
                Debug.LogWarning(
                    $"Pergunta OK/Cancel não possui diálogo Linux ainda: {question}"
                );

                return DialogResult.OK;
            }

            return MessageBox.Show(
                question,
                "",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning
            );
        }
    }
}
