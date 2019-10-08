﻿using ChatProgram.Classes;
using NotificationsExtensions;
using NotificationsExtensions.Toasts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace ChatProgram.Client
{
    public partial class ClientForm : Form
    {
        public ClientForm()
        {
            InitializeComponent();
            Client = new ClientConnection(this);
            Logger.LogMsg("Client started");
        }

        public ClientConnection Client;

        public void Connect(IPAddress ip)
        {
            Logger.LogMsg($"Connecting {ip}");
            Client.Client.Connect(ip, Program.Port);
            if(Client.Client.Connected)
            {
                Logger.LogMsg("Connected");
                Client.NewMessage += Client_NewMessage;
                Client.NewUser += Client_UserListChange;
                Client.IdentityKnown += Client_UserListChange;
                Client.UserUpdate += Client_UserListChange;
                Client.Send(Environment.UserName);
                Logger.LogMsg("Sent username, opened listener");
                Client.Listen();
                Common.Users[999] = new User() { Id = 999, Name = "Server" };
                this.Activated += ClientForm_Activated;
            } else
            {
                Logger.LogMsg("Failed connect");
            }
        }

        Label createLabelFor(User u, ref int y)
        {
            int x = 5;
            var label = new Label();
            label.Tag = u;
            label.Text = $"#{u.Id} {u.Name}";
            label.Location = new Point(x, y);
            y += 30;
            return label;
        }

        private void Client_UserListChange(object sender, Classes.User e)
        {
            Common.Users[e.Id] = e;
            gbUsers.Controls.Clear();
            var users = Common.Users.OrderBy(x => x.Key);
            int y = 15;
            foreach (var user in users)
            {
                var lbl = createLabelFor(user.Value, ref y);
                lbl.TextAlign = ContentAlignment.TopCenter;
                gbUsers.Controls.Add(lbl);
                lbl.Click += user_click;
            }
        }

        private void user_click(object sender, EventArgs e)
        {
            if (sender is Label lbl && e is MouseEventArgs me)
            {
                if (me.Button == MouseButtons.Right)
                    lbl.BackColor = Color.LightBlue;
            }
        }

        Label getLabelFor(Classes.Message message, ref int y)
        {
            int y_offset = y;
            if (this.AutoScrollOffset.Y != 0)
                y_offset -= this.AutoScrollOffset.Y;
            var lbl = new Label();
            lbl.Text = $"{message.Author.Name}: {message.Content}";
            lbl.Tag = message;
            lbl.AutoSize = true;
            lbl.MaximumSize = new Size(gbMessages.Size.Width - 15, 0);
            lbl.Location = new Point(5, y);
            y += 20;
            return lbl;
        }
        private void ClientForm_Activated(object sender, EventArgs e)
        {
            uint latestMax = LAST_SEEN_MESSAGE;
            foreach(var control in gbMessages.Controls)
            {
                if(control is Label lbl)
                {
                    if(lbl.Tag is Classes.Message msg)
                    {
                        lbl.BackColor = Color.FromKnownColor(KnownColor.Control);
                        if(msg.Id > latestMax) // since no guarante of order
                            latestMax = msg.Id;
                    }
                }
            }
            if(LAST_SEEN_MESSAGE < latestMax) // hasnt changed in mean time
                LAST_SEEN_MESSAGE = latestMax;
        }
        int MESSAGE_Y = 5;
        uint LAST_SEEN_MESSAGE = 0;
        private void Client_NewMessage(object sender, Classes.Message e)
        {
            var lbl = getLabelFor(e, ref MESSAGE_Y);
            lbl.Click += Lbl_Click;
            if(Form.ActiveForm == this)
            {
                LAST_SEEN_MESSAGE = e.Id;
            } else
            {
                lbl.BackColor = Color.LightCoral;
            }
            this.gbMessages.Controls.Add(lbl);
            int charactors = lbl.Text.Length;
            var rows = charactors / 80d;
            while(rows > 0)
            {
                MESSAGE_Y += 5;
                rows--;
            }
            this.Text = MESSAGE_Y.ToString();

            ToastContent content = new ToastContent()
            {
                Launch = $"{e.Id}",
                Visual = new ToastVisual
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        AppLogoOverride = new ToastGenericAppLogo
                        {
                            HintCrop = ToastGenericAppLogoCrop.Circle,
                            Source = "http://messageme.com/lei/profile.jpg"
                        },
                        Children =
                        {
                            new AdaptiveText {Text = $"New message from {e.Author.Name}" },
                            new AdaptiveText {Text = e.Content }
                        },
                        Attribution = new ToastGenericAttributionText
                        {
                            Text = "Alert"
                        },
                    }
                },
                Actions = new ToastActionsCustom()
                {
                    Inputs =
                    {
                        new ToastTextBox("tbReply")
                        {
                            PlaceholderContent = "Type a response"
                        }
                    },
                    Buttons =
                    {   
                        new ToastButton("reply", "reply")
                        {
                            ActivationType = ToastActivationType.Background,
                            ImageUri = "Assets/QuickReply.png",
                            TextBoxId = "tbReply"
                        }
                    }
                },
                Audio = new ToastAudio()
                {
                    Src = new Uri("ms-winsoundevent:Notification.IM")
                }
            };
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(content.GetContent());


            // Generate WinRT notification
            var toast = new ToastNotification(doc);
            
            // Display toast
            ToastNotificationManager.CreateToastNotifier("CheAle14.ChatProgram.Client").Show(toast);
        }

        private void Lbl_Click(object sender, EventArgs e)
        {
            if(sender is Label lbl && lbl.Tag is Classes.Message msg)
            {
                MessageBox.Show(msg.Content, $"#{msg.Id} from {msg.Author.Name}");
            }
        }

        private void txtMessage_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                var msg = new Classes.Message() { Author = Client.CurrentUser, Content = txtMessage.Text };
                txtMessage.Text = "";
                var pcket = new Packet(PacketId.SendMessage, msg.ToJson());
                Client.Send(pcket.ToString());
            }
        }

        private void ClientForm_Load(object sender, EventArgs e)
        {

        }
    }
}
