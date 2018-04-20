﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MyClient
{
    public partial class Form1 : Form
    {
         Socket tcpSocket = new Socket(AddressFamily.InterNetwork,
                                        SocketType.Stream,
                                        ProtocolType.Tcp);
        // we need this delegate to change richTextBox and comboBox
        public delegate void Action(string text);

        //get the local address of our machine
        IPAddress LocalAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0];

        private List<string> listOfUsersToSend = new List<string>();

        public Form1()
        {
            InitializeComponent();
           
        }

        //we want to get  server local address 
        public EndPoint GetServerAddress()
        {
            //buffer for the received message UPD: need to be updated every time
           var takenReceivedBytes = new byte[100];
            var receivedBytesFromMulticast = new byte[500];

            //multicast address and port
            IPAddress mcastAddress = IPAddress.Parse("224.12.12.12");
            int  mcastPort = 4000;

            //we need multicast socket to send both  addresses between host and clients
            Socket mcastSocket = new Socket(AddressFamily.InterNetwork,
                                        SocketType.Dgram,
                                        ProtocolType.Udp);
            //things that were described at https://msdn.microsoft.com/ru-ru/library/system.net.sockets.multicastoption(v=vs.110).aspx
            EndPoint serverEndPoint = new IPEndPoint(mcastAddress, mcastPort);
            var multicastEndPoint = new IPEndPoint(mcastAddress, mcastPort);   
             var mcastOption = new MulticastOption(mcastAddress, LocalAddress);
            mcastSocket.SetSocketOption(SocketOptionLevel.IP,
                                        SocketOptionName.AddMembership,
                                        mcastOption);
            //now we want to send ANY information to the server
            mcastSocket.SendTo(Encoding.UTF8.GetBytes("Useless "), multicastEndPoint);

            // we need to 'cut' the buffer(problem with zero bytes)
            takenReceivedBytes = receivedBytesFromMulticast.Take(mcastSocket.ReceiveFrom(receivedBytesFromMulticast, ref serverEndPoint)).ToArray();
            string stringEndpoint = Encoding.UTF8.GetString(takenReceivedBytes);
            char separator = ':';
            string[] strings = stringEndpoint.Split(separator);
            IPAddress serverAddress = IPAddress.Parse(strings[0]);
            EndPoint newServerEP = new IPEndPoint(serverAddress, mcastPort);

            // now we want to connect our tcp socket to the server
            return newServerEP;

        }

        //sending message via tcp
        void SendMessage(XDocument document)
        {
            tcpSocket.Send(GetBytesToSendFromDocument(document));
        }

        //here we want to define the type of the received message
        // and take the information we need
        public string DefineMessage(byte[] bytes)
        { 

            var receivedMessage = GetDocumentFromReceivedBytes(bytes);
            //creating the dictionary from the xml document to parse data correctly
            var dictionary = receivedMessage.Element("msg").Elements("data").ToDictionary
            (elem => elem.Attribute("key").Value,
            elem => elem.Attribute("value").Value);

            switch (dictionary["Type"])
            {
                case "Send":
                    return $"{dictionary["Author"]} написал:  {dictionary["Text"]}";
                case "Name":
                    return $"{dictionary["Name"]} присоединился к чату";
                case "List":
                    
                    // here we use this Action to write data into out control
                    Action<string> comboBoxAction = (collection) =>
                    {
                        var charSeparators = new char[] { ',' };
                        var listOfUsers = collection.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);
                        
                        comboBox1.Items.Clear();

                        foreach (var user in listOfUsers)
                            comboBox1.Items.Add(user);


                    };

                    Invoke(comboBoxAction, dictionary["List"]);
                    return $"Список юзеров обновлен!";
                case "PrivateSend":
                    return $"{dictionary["Author"]} написал вам  {dictionary["Text"]}";
            }

            return "something went wrong!";

        }

   
        private  string  ReceiveBroadcastMessages()
        {
            var receivedBytes = new byte[500];
            var takenBytes = receivedBytes.Take(tcpSocket.Receive(receivedBytes)).ToArray();
            return DefineMessage(takenBytes);
        }

        //private byte[] GetTrimmedBytes(byte[] receivedBytes, int receivedCount)
        //{
           
        //    return receivedBytes.Take(tcpSocket.Receive(receivedBytes)).ToArray();

        //}
        private XDocument GetDocumentFromString(string message)
        {
            var sendMessageDocument = new XDocument(
                new XElement("msg",
                    new XElement("data", new XAttribute("key", "Type"), new XAttribute("value", "Send")),
                    new XElement("data", new XAttribute("key", "Text"), new XAttribute("value", message))));
            return sendMessageDocument;

        }
        private byte[] GetBytesToSendFromDocument(XDocument document)
        {
            return Encoding.UTF8.GetBytes(document.ToString());
        }
        private XDocument GetDocumentFromReceivedBytes(byte[] bytes)
        {
            return XDocument.Parse(Encoding.UTF8.GetString(bytes));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            tcpSocket.Connect(GetServerAddress());
            timer1.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            
            SendMessage(GetDocumentFromString(textBox1.Text));
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Action<string> myAction =  (str) => richTextBox1.AppendText(str +"\n");
            new Thread(() =>
            {
                this.Invoke(myAction, ReceiveBroadcastMessages());
            }
             ).Start();
            
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
           if(!listOfUsersToSend.Contains(comboBox1.SelectedItem.ToString()))
               listOfUsersToSend.Add(comboBox1.SelectedItem.ToString());
            listView1.Items.Clear();
            foreach (var user in listOfUsersToSend)
                listView1.Items.Add(user);

        }

        private void button3_Click(object sender, EventArgs e)
        {
            string userString = "";
            foreach (ListViewItem user in listView1.Items)
                userString += user.Text + ",";
            var sendMessageDocument = new XDocument(
                new XElement("msg",
                    new XElement("data", new XAttribute("key", "Type"), new XAttribute("value", "PrivateSend")),
                    new XElement("data", new XAttribute("key", "Persons"), new XAttribute("value",userString)),
                    new XElement("data", new XAttribute("key", "Text"), new XAttribute("value", textBox2.Text))
                    ));
            tcpSocket.Send(GetBytesToSendFromDocument(sendMessageDocument));
        }
    }
}
