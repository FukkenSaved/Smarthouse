﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Collections.Concurrent;


namespace Smarthouse
{
    class Network : ThreadControllable
    {

        const byte append_length = 4;//uint - 32 bytes
        const byte md5_length = 32;// md5 hash is always 32 symbols length


        bool isWorking = false;

        bool server;
        IPAddress server_ip;
        int port;


        #region Server
        ConcurrentDictionary<string, Session> sessions = new ConcurrentDictionary<string, Session>();
        Random rnd;
        Socket reciever;

        public Network(int port)
        {
            rnd = new Random();
            server = true;
            server_ip = IPAddress.Any;
            reciever = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint myEP = new IPEndPoint(IPAddress.Any, port);
            reciever.Bind(myEP);
            reciever.Listen(10);
        }
        void endAccept(System.IAsyncResult ar)
        {
            Socket sck = reciever.EndAccept(ar);//now sck is our socket connected to client
            String login;
            reciever.BeginAccept(endAccept, null);//Continue listening to other clients
            #region authorization
            if (auth(sck, out login))
            {
                // Console.WriteLine("+ "+login + " acces granted!");
                Program.core.ud.GetUser(login).Status = (byte)UserDomain.Statuses.Net;
            }
            else
            {
                //Console.WriteLine("+ " +login + " wrong password or already connected!");
                sck.Disconnect(false);
                sck.Close();//reject this user
                return;
            }
            #endregion
            Console.WriteLine(login +" connected");
            Console.WriteLine(sessions[login].Crypt.key);
            Console.WriteLine("______________");

            StateObject so = new StateObject();
            so.workSocket = sck;
            sck.BeginReceive(so.buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, EndRecieve, so);
        }

        void EndRecieve(System.IAsyncResult ar)
        {
            //StateObject so = (StateObject)ar.AsyncState;
            //byte pin = so.buffer[0];
            //bool type = !(so.buffer[1] < 128);
            //byte[] value;
            //if (type)
            //{
            //    //неудача. Нас ждет анальная боль. Много байт. К хуям бесперебойность!
            //    byte[] temp = new byte[3];
            //    so.workSocket.Receive(temp, 0, 3, SocketFlags.None);//качаем ещё 3 бита размера
            //    byte[] s = new byte[4] { temp[0], temp[1], temp[2], (byte)(so.buffer[1] - 128) };//здесь у нас будет храниться размер
            //    UInt32 size = BitConverter.ToUInt32(s, 0);
            //    value = new byte[size];
            //    so.workSocket.Receive(value, SocketFlags.None);//принимаем невъебенно большой файл и забиваем его в value 
            //}
            //else
            //{
            //    value = new byte[] { so.buffer[1] }; //збc, всего 1 байт.
            //}

            //Smarthouse.output.SetValue(pin, value);//sending 

            //so.workSocket.BeginReceive(so.buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, EndRecieve, so); //вылетает ошибка при обрывании коннекта
        }

        #region Auth
        bool auth(Socket sck, out string login)
        {
            User user;
            bool success = false; ;
            login = recieveLogin(sck);
            uint append = (uint)(rnd.Next(int.MinValue, int.MaxValue) + int.MaxValue);  // Generate append 


            if (Smarthouse.Program.core.ud.Contains(login)) //if there is such user
            {
                user = Smarthouse.Program.core.ud.GetUser(login);
                sendAppend(sck, append);                           //send append
                success = check(sck, Crypt.generateCheckKey(user.Pass, append)); // check key
                if (success)
                {
                    #region Adding login to the sessions
                    if (!sessions.TryAdd(login, new Session(append, sck, new Crypt(append, user.Pass))))
                    {
                        //Console.WriteLine("+_______ Error! \"" + login + "\" is already on the session list.");
                        success = false;
                    }
                    #endregion
                }
                else
                {
                    //Console.WriteLine("+_______ Error! \"" + login + "\" wrong password.");
                }
            }
            else
            {
                //Console.WriteLine("+_______ Error! No user named \"" + login + "\"");
                success = false;
            }
            return success;
        }
        string recieveLogin(Socket sck)
        {
            byte[] length = new byte[1];
            sck.Receive(length);//recieving length
            byte[] login_buff = new byte[length[0]];
            sck.Receive(login_buff);//recieving login
            return Encoding.UTF8.GetString(login_buff);
        }
        void sendAppend(Socket sck, uint append)
        {
            sck.Send(BitConverter.GetBytes(append));//sending login
        }
        bool check(Socket sck, string check_key)
        {
            byte[] recieved_check_key = new byte[md5_length];
            sck.Receive(recieved_check_key);
            return Encoding.UTF8.GetString(recieved_check_key) == check_key;
        }
        #endregion
        #endregion
        #region Client
        Socket sck_client;
        string login_client;
        Crypt crypt_client;
        uint append_client;
        string client_password;
        public Network(string ip, int port, string login, string password)
        {
            server = false;
            server_ip = IPAddress.Parse(ip);
            client_password = password;
            this.port = port;
            login_client = login;
            sck_client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        }
        void endConnect(System.IAsyncResult ar)
        {
            sck_client.EndConnect(ar);
            //authorization

            if (auth(login_client) && sck_client.Connected==true)
            {
                //Console.WriteLine("- " + login_client + " connected!");
            }
            else
            {
               // Console.WriteLine("- " + login_client + " failed!"); 
            }
            
            Console.WriteLine(login_client);
            Console.WriteLine(crypt_client.key);
            Console.WriteLine("______________");


        }
        #region Auth
        bool auth(string login)
        {
            try
            {
                sendLogin(sck_client, login);               //sending login
                append_client = recieveAppend(sck_client);   //recieve append
                crypt_client = new Crypt(append_client, client_password);//create Crypt object
                sendCheckKey(sck_client, Crypt.generateCheckKey(client_password, append_client)); //send generated key

                return true;

            }
            catch
            {
                return false;
            };


        }
        void sendLogin(Socket sck, string login)
        {
            sck.Send(new byte[] { (byte)login.Length });//sending login's length
            sck.Send(Encoding.UTF8.GetBytes(login));//sending login
        }
        uint recieveAppend(Socket sck)
        {
            byte[] append_buff = new byte[append_length];
            sck.Receive(append_buff);//recieving login
            return BitConverter.ToUInt32(append_buff, 0);
        }
        void sendCheckKey(Socket sck, string checkkey)
        {
            sck.Send(Encoding.UTF8.GetBytes(checkkey));//sending check key
        }
        #endregion
        #endregion


       



        #region ThreadControllable
        public void Start()
        {
            if (!isWorking)
            {
                isWorking = true;
                if (server)
                {
                    reciever.BeginAccept(endAccept, null);
                }
                else
                {
                    sck_client.BeginConnect(server_ip, port, endConnect, null);
                }
            }
        }
        public void Stop()
        {
            isWorking = false;
        }
        public bool IsWorking
        {
            get
            {
                return isWorking;
            }
        }
        #endregion
    }

    class StateObject
    {
        public Socket workSocket = null;
        public const int BUFFER_SIZE = 2;
        public byte[] buffer = new byte[BUFFER_SIZE];
    }

    class Session
    {
        uint Append;
        Socket Sck;
        public Crypt Crypt;

        public Session()
        {
            Append = 0;
            Crypt = null;
            Sck = null;
        }

        public Session(uint append, Socket sck, Crypt crypt)
        {
            Append = append;
            Sck = sck;
            Crypt = crypt;
        }

    }
}
