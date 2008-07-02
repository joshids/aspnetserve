/************************************************************************
 * Copyright (c) 2006-2008, Jason Whitehorn (jason.whitehorn@gmail.com)
 * All rights reserved.
 * 
 * Source code and binaries distributed under the terms of the included
 * license, see license.txt for details.
 ************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using aspNETserve.Core;

namespace aspNETserve {
    /// <summary>
    /// Handles incoming HTTP communications, and passes them into the AppDomain 
    /// for processing by the Host.
    /// </summary>
    public class Server : IServer {
        private string _appId;
        private int _port;
        private IPAddress _endPoint;
        private string _virtualDir;
        private string _physicalDir;
        private Socket _sock;
        private DomainHook _host;
        private ApplicationManager _appManager;
        private IDictionary<string, string> _serverVariables;
        private ServerStatus _status = ServerStatus.Stopped;

        protected Server() { }

        public Server(IPAddress endPoint, string virtualDir, string physicalDir, int port) {
            _endPoint = endPoint;
            _virtualDir = virtualDir;
            _physicalDir = physicalDir;
            if (!_physicalDir.EndsWith("\\"))
                _physicalDir += "\\";
            _port = port;
            _appId = Guid.NewGuid().ToString(); //generate a new application id
            _appManager = ApplicationManager.GetApplicationManager();
            PrepareServerVariables();
            ConfigureResponses();
        }

        public virtual void Start() {
            /*
             * Create a new AppDomain containing the Host, and begin handling incoming requests.
             */ 
            if (_sock != null) 
                return;
            SetStatus(ServerStatus.Starting);
            try {
                _host = _appManager.CreateObject(_appId, typeof(DomainHook), _virtualDir, _physicalDir, false) as DomainHook;
                _host.Configure(typeof(AspNetWorker), new object[]{_virtualDir, _physicalDir, _serverVariables});
                _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _sock.Bind(new IPEndPoint(_endPoint, _port));
                _sock.Listen(100);
                _sock.BeginAccept(new AsyncCallback(ProcessRequest), _sock);
            } catch {
                SetStatus(ServerStatus.Stopped);
                throw;
            }
            SetStatus(ServerStatus.Running);
    
        }

        public virtual void Stop() {
            if (_sock != null) {
                SetStatus(ServerStatus.ShuttingDown);
                _sock.Close();  //stop accepting connections
                _host.Dispose();    //this will call InitiateShutdown from within the app domain
            }
            _sock = null;
            SetStatus(ServerStatus.Stopped);
        }

        public virtual ServerStatus Status {
            get { return _status; }
        }

        public static string Version() {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        public virtual void Dispose() {
            if (_status == ServerStatus.Running)
                Stop();
        }

        protected virtual void PrepareServerVariables() {
            _serverVariables = new Dictionary<string, string>();
            _serverVariables.Add("SERVER_SOFTWARE", "aspNETserve/" + Version());
        }

        protected virtual void ConfigureResponses() {
//            Response error403Forbidden = new Response();
//            error403Forbidden.StatusDescription = "Forbidden";
//            error403Forbidden.StatusCode = (int)HttpResponseCode.NotFound;
//            error403Forbidden.RawData = Encoding.UTF8.GetBytes(@"
//                        <html>
//                        <head><title>403 - Forbidden</title></head>
//                        <body>
//                        <h1>403 - Forbidden</h1><br/>
//                        The server is actively refusing access to the specified resource.<br/>
//                        <hr/>
//                        aspNETserve/" + Version() + @"
//                        </body>
//                        </html>
//                    ");
//            Response.Error403Forbidden = error403Forbidden;
        }

        /// <summary>
        /// This method processes the socket request. This is the first entry point for requested entering the aspNETserve
        /// server.
        /// </summary>
        /// <param name="async">The IAsyncResult used to aquire to Socket.</param>
        protected virtual void ProcessRequest(IAsyncResult async) {
            Socket com = null;
            try {
                com = _sock.EndAccept(async);


                _sock.BeginAccept(new AsyncCallback(ProcessRequest), _sock);
                EndPoint remoteIp = com.RemoteEndPoint;

                HttpSocketRequestResponse transaction = new HttpSocketRequestResponse(com);
                _host.ProcessTransaction(transaction);
                byte[] rawResponse = transaction.Response.ToHttpResponse();
                com.Send(rawResponse);
                com.Close();    //this breaks "Connection: Keep-Alive" requests.

            } catch {
                if (com != null) {
                    try {
                        com.Close();
                    } catch { }
                }
            }
        }

        protected virtual void SetStatus(ServerStatus status) {
            _status = status;
        }

        protected string AppId {
            get { return _appId; }
            set { _appId = value; }
        }

        protected int Port {
            get { return _port; }
            set { _port = value; }
        }

        protected IPAddress EndPoint {
            get { return _endPoint; }
            set { _endPoint = value; }
        }

        protected string VirtualDir {
            get { return _virtualDir; }
            set { _virtualDir = value; }
        }

        protected string PhysicalDir {
            get { return _physicalDir; }
            set { _physicalDir = value; }
        }

        protected Socket Sock {
            get { return _sock; }
            set { _sock = value; }
        }

        protected DomainHook Host {
            get { return _host; }
            set { _host = value; }
        }

        protected ApplicationManager AppManager {
            get { return _appManager; }
            set { _appManager = value; }
        }

        protected IDictionary<string, string> ServerVariables {
            get { return _serverVariables; }
            set { _serverVariables = value; }
        }

    }
}