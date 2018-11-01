#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (Class1.cs) is part of Oetools.Utilities.Test.
// 
// Oetools.Utilities.Test is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.Utilities.Test is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities.Test. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace Oetools.Utilities.Test.Archive.HttpFileServer {

    public class SimpleStupidHttpFileServer {
        private const int BufferSize = 1024 * 8;
        private Thread _thread;
        private volatile bool _threadActive;

        private HttpListener _listener;
        private string _rootPath;
        private string _ip;
        private int _port;

        public SimpleStupidHttpFileServer(string rootPath, string ip, int port) {
            _rootPath = rootPath;
            _ip = ip;
            _port = port;
        }

        public void Start() {
            if (_thread != null) {
                throw new Exception("WebServer already active.");
            }
            _thread = new Thread(Listen);
            _thread.Start();
        }

        public void Stop() {
            // stop thread and listener
            _threadActive = false;
            if (_listener != null && _listener.IsListening)
                _listener.Stop();

            // wait for thread to finish
            if (_thread != null) {
                _thread.Join();
                _thread = null;
            }

            // finish closing listener
            if (_listener != null) {
                _listener.Close();
                _listener = null;
            }
        }

        private void Listen() {
            _threadActive = true;

            // start listener
            try {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://{_ip}:{_port}/");
                _listener.Start();
            } catch (Exception e) {
                Console.WriteLine($"ERROR: {e.Message}.");
                _threadActive = false;
                throw;
            }

            // wait for requests
            while (_threadActive) {
                try {
                    var context = _listener.GetContext();
                    if (!_threadActive)
                        break;
                    try {
                        ProcessContext(context);
                    } catch (Exception e) {
                        var data = Encoding.Default.GetBytes(e.Message);
                        context.Response.OutputStream.Write(data, 0, data.Length);
                        context.Response.OutputStream.Close();
                    }
                } catch (HttpListenerException e) {
                    if (e.ErrorCode != 995)
                        Console.WriteLine($"ERROR: {e.Message}.");
                    _threadActive = false;
                } catch (Exception e) {
                    Console.WriteLine($"ERROR: {e.Message}.");
                    _threadActive = false;
                }
            }
        }

        private void ProcessContext(HttpListenerContext context) {
            
            // get filename path
            string filePath = context.Request.Url.AbsolutePath;
            if (!string.IsNullOrEmpty(filePath) && filePath.Length > 1) {
                // handle spaces with urldecode
                filePath = WebUtility.UrlDecode(filePath.Substring(1));
            }

            if (string.IsNullOrEmpty(filePath)) {
                context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                var data = Encoding.ASCII.GetBytes("Path not specified.");
                context.Response.OutputStream.Write(data, 0, data.Length);
                context.Response.OutputStream.Close();
                return;
            }
            
            filePath = Path.Combine(_rootPath, filePath);
            
            switch (context.Request.HttpMethod) {
                case "PUT":
                    // curl -v -H "Expect:" -u admin:admin123 --upload-file myfile http://127.0.0.1:8084/repository/raw-hoster/remotefile.txt --proxy 127.0.0.1:8888
                    
                    bool expect = false;
                    var headers = context.Request.Headers;
                    var headersKeys = headers.AllKeys;
                    for (int i = 0; i < headersKeys.Length; i++) {
                        if (headersKeys[i].Equals("Expect", StringComparison.OrdinalIgnoreCase)) {
                            if (headers.GetValues(headersKeys[i])?.ToList().Exists(s => s.Equals("100-continue", StringComparison.OrdinalIgnoreCase)) ?? false) {
                                expect = true;
                                break;
                            }
                        }
                    }
                    
                    // TODO : handle the continue... I can't figure out how to do this one
                    if (expect) {
                        context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                        var data = Encoding.ASCII.GetBytes("Can't handle continue.");
                        context.Response.OutputStream.Write(data, 0, data.Length);
                        context.Response.OutputStream.Close();
                        context.Response.Abort();
                        return;
                    }
                    
                    // create necessary dir
                    var dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                        Directory.CreateDirectory(dir);
                    }

                    using (var fileStr = File.OpenWrite(filePath)) {
                        byte[] buffer = new byte[BufferSize];
                        int nbBytesRead;
                        while ((nbBytesRead = context.Request.InputStream.Read(buffer, 0, buffer.Length)) > 0) {
                            fileStr.Write(buffer, 0, nbBytesRead);
                        }
                    }
                 
                    // finish
                    context.Response.StatusCode = (int) HttpStatusCode.Created;
                    context.Response.AddHeader("Location", context.Request.RawUrl);
                    break;
                
                case "GET":
                    // curl -v -u admin:admin123 -o mydownloadedfile http://127.0.0.1:8084/repository/raw-hoster/remotefile.txt --proxy 127.0.0.1:8888
                    
                    if (!File.Exists(filePath)) {
                        context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                    } else {
                        using (var stream = File.OpenRead(filePath)) {
                            // get mime type
                            context.Response.ContentType = "application/octet-stream";
                            context.Response.ContentLength64 = stream.Length;
                            
                            byte[] buffer = new byte[BufferSize];
                            int nbBytesRead;
                            while ((nbBytesRead = stream.Read(buffer, 0, buffer.Length)) > 0) {
                                context.Response.OutputStream.Write(buffer, 0, nbBytesRead);
                            }
                            
                            context.Response.OutputStream.Flush();
                        }
                        
                        context.Response.StatusCode = (int) HttpStatusCode.OK;
                        context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
                        context.Response.AddHeader("Last-Modified", File.GetLastWriteTime(filePath).ToString("r"));
                    }
                    break;
                
                case "DELETE":
                    // curl -v -u admin:admin123 -X DELETE http://127.0.0.1:8084/repository/raw-hoster/remotefile.txt --proxy 127.0.0.1:8888
                    
                    if (!File.Exists(filePath)) {
                        context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                    } else {
                        File.Delete(filePath);
                        context.Response.StatusCode = (int) HttpStatusCode.NoContent;
                    }
                    break;
                default:
                    throw new Exception($"Unknown http verb : {context.Request.HttpMethod}.");
            }
            
            context.Request.InputStream.Close();
            context.Response.Close();
            
        }
    }
}