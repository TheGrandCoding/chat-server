﻿using ChatProgram.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatProgram.Server
{
    public class ConnectionManager
    {
        public ServerForm Form;
        public ConnectionManager(ServerForm form)
        {
            Form = form;
        }
        TcpListener Server = new TcpListener(IPAddress.Loopback, Program.Port);

        Dictionary<uint, Connection> Connections = new Dictionary<uint, Connection>();

        /// <summary>
        /// Client has sent this <see cref="Message"/> to be broadcasted
        /// </summary>
        public event EventHandler<Message> NewMessage;

        public void _internalServerMessage(Message m)
        {
            if(Form.InvokeRequired)
            {
                Form.Invoke(new Action(() =>
                {
                    _internalServerMessage(m);
                }));
                return;
            }
            NewMessage?.Invoke(this, m);
        }

        /// <summary>
        /// A new client has connected, this is it.
        /// </summary>
        public event EventHandler<User> NewUser;

        public void Start()
        {
            Server.Start();
            newClientThread = new Thread(newClientHandle);
            newClientThread.Start();
        }

        public void Broadcast(Packet packet)
        {
            foreach(var conn in Connections.Values)
            {
                conn.Send(packet.ToString());
            }
        }

        bool _listen = true;
        public bool Listening {  get
            {
                return _listen;
            }  set
            {
                _listen = value;
            }
        }

        Thread newClientThread;
        void newClientHandle()
        {
            do
            {
                TcpClient client = Server.AcceptTcpClient();
                Logger.LogMsg("New TcpClient connected");
                var stream = client.GetStream();
                var bytes = new Byte[client.ReceiveBufferSize];
                stream.Read(bytes, 0, bytes.Length);
                var data = Encoding.UTF8.GetString(bytes);

                data = data.Replace("\0", "").Trim();
                data = data.Substring(1, data.Length - 2);

                var nClient = new User();
                nClient.Id = Common.USER_ID++;
                nClient.Name = data;
                Logger.LogMsg($"New User: '{data}' ({nClient.Id})");
                Common.Users[nClient.Id] = nClient;
                var conn = new Connection(nClient.Id.ToString());
                Connections[nClient.Id] = conn;
                conn.Client = client;
                conn.Listen();
                conn.Receieved += Conn_Receieved;
                var identity = new Packet(PacketId.GiveIdentity, nClient.ToJson());
                conn.Send(identity.ToString());

                foreach(var id in Connections.Keys)
                {
                    if(Common.Users.TryGetValue(id, out var user))
                    {
                        var packet = new Packet(PacketId.UserUpdate, user.ToJson());
                        conn.Send(packet.ToString());
                    }
                }

                Form.Invoke(new Action(() =>
                {
                    NewUser?.Invoke(this, nClient);
                }));
            } while (_listen);
        }

        private void Conn_Receieved(object sender, string e)
        {
            if(sender is Connection connection)
            {
                if(uint.TryParse(connection.Reference, out var id))
                {
                    if(Common.Users.TryGetValue(id, out var user))
                    {
                        Logger.LogMsg($"From {user.Name}({user.Id}): {e}");
                        Form.Invoke(new Action(() => {
                            var packet = new Packet(e);
                            HandleConnMessage(connection, user, packet);
                        }));
                    } else
                    {
                        Logger.LogMsg($"No User ({id}): {e}", LogSeverity.Warning);
                    }
                } else
                {
                    Logger.LogMsg($"No Reference ({connection.Reference}): {e}");
                }
            }
        }

        private void HandleConnMessage(Connection connection, User user, Packet packet)
        {
            if(packet.Id == PacketId.SendMessage)
            {
                var msg = new Message();
                msg.FromJson(packet.Information);
                msg.Id = Common.MESSAGE_ID++;
                NewMessage?.Invoke(this, msg);
                var pong = new Packet(PacketId.NewMessage, msg.ToJson());
                Broadcast(pong);
            }
        }
    }
}
