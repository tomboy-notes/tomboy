using Gtk;
using Mono.Unix;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tomboy.PassEncrypt
{
    class PasswordWindow : Window
    {
        public Entry firstEntry = new Entry();
        public Entry secondEntry = new Entry();
        VBox vbox = new VBox();
        public TaskCompletionSource<string> EnteredPass = new TaskCompletionSource<string>();
        private bool WithConfirmation = true;

        public PasswordWindow(bool withConfirmation) : base(WindowType.Toplevel)
        {
            WithConfirmation = withConfirmation;
            firstEntry.GrabFocus();
            firstEntry.Text = Catalog.GetString("Please enter password");
            firstEntry.Changed += Entry_Changed;
            firstEntry.Activated += Entry_Activated;
            vbox.PackStart(firstEntry, false, true, 2);

            if (withConfirmation)
            {
                secondEntry.Text = Catalog.GetString("Reenter password");
                secondEntry.Changed += Entry_Changed;
                secondEntry.Activated += Entry_Activated;
            }
            else
            {
                secondEntry.Text = Catalog.GetString("Decoded text");
                secondEntry.Visibility = true;
                secondEntry.IsEditable = false;
            }
            vbox.PackStart(secondEntry, false, true, 2);
            
            this.Add(vbox);
            this.Title = Catalog.GetString("Enter Password");
            this.SetPosition(WindowPosition.Mouse);
            this.FocusOutEvent += PasswordWindow_FocusOutEvent;
        }

        private void PasswordWindow_FocusOutEvent(object o, FocusOutEventArgs args)
        {
            EnteredPass.TrySetResult(string.Empty);
            this.Destroy();
        }

        private void Entry_Changed(object sender, EventArgs e)
        {
            firstEntry.Changed -= Entry_Changed;
            firstEntry.Text = string.Empty;
            firstEntry.Visibility = false;

            secondEntry.Changed -= Entry_Changed;
            secondEntry.Text = string.Empty;
            secondEntry.Visibility = false;
        }

        private void Entry_Activated(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(firstEntry.Text))
            {
                firstEntry.GrabFocus();
                return;
            }
            if (WithConfirmation && (string.IsNullOrWhiteSpace(secondEntry.Text) || secondEntry.Text.Trim() != firstEntry.Text.Trim()))
            {
                secondEntry.GrabFocus();
                return;
            }
            else
            {
                firstEntry.IsEditable = false;
                secondEntry.IsEditable = false;

                firstEntry.Activated -= Entry_Activated;
                secondEntry.Activated -= Entry_Activated;

                EnteredPass.SetResult(firstEntry.Text.Trim());

                if (WithConfirmation)
                    this.Destroy();
                return;
            }
        }

        public void ShowNonEditableText(string text)
        {
            secondEntry.Text = text;
            secondEntry.Visibility = true;
            secondEntry.IsEditable = false;
            secondEntry.GetClipboard(Gdk.Selection.Clipboard).Text = text;
        }

        public async Task<string> GetPassword()
        {
            return await this.EnteredPass.Task;
        }
    }
}
