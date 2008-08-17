﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace aspNETserve {
    /// <summary>
    /// Represents a WebApplicationPackage archive.
    /// </summary>
    public class WebApplicationPackage : IDisposable {
        private bool _isOpen;
        private readonly bool _ownsStream;
        private readonly Stream _stream;
        private const int _bufferSize = 4096;
        private string _physicalPath;

        /// <summary>
        /// Constructs an instance of WebApplicationPackage.
        /// This constructor assumes ownership of the stream, and will
        /// close it when the WebApplicationPackage is disposed.
        /// </summary>
        /// <param name="stream">A Stream containing the WAP archive.</param>
        public WebApplicationPackage(Stream stream) : this(stream, true) {}

        /// <summary>
        /// Constructs an instance of WebApplicationPackage.
        /// </summary>
        /// <param name="stream">A Stream containing the WAP archive.</param>
        /// <param name="ownsStream">If true the stream will be closed when the 
        /// instance of WebApplicationPackage is disposed, otherwise it will be left open.
        /// </param>
        public WebApplicationPackage(Stream stream, bool ownsStream) {
            _stream = stream;
            _ownsStream = ownsStream;
        }

        /// <summary>
        /// releases the unmanaged resources allocated by the current
        /// WebApplicationPackage instance.
        /// </summary>
        public virtual void Dispose() {
            if(_ownsStream)
                _stream.Close();
            if(IsOpen)
                DeleteDirectory(_physicalPath);
            IsOpen = false;
        }

        /// <summary>
        /// Opens the WebApplicationPackage for reading.
        /// </summary>
        public virtual void Open() {
            if(IsOpen)
                throw new Exception("Cannot open WAP, it is already opened.");
            IsOpen = true;
            string directory = (Guid.NewGuid()).ToString();
            _physicalPath = Path.Combine(Path.GetTempPath(), directory);
            Directory.CreateDirectory(_physicalPath);

            ExtractWapToPath(_physicalPath);
        }

        /// <summary>
        /// Returns the path on disk to the contents
        /// of an opened WebApplicationPackage.
        /// </summary>
        public virtual string PhysicalPath {
            get {
                if(!IsOpen)
                    throw new Exception("The WAP is not opened yet.");
                return _physicalPath;
            }
        }

        /// <summary>
        /// Determines if the WebApplicationPackage has been opened
        /// </summary>
        public virtual bool IsOpen {
            get { return _isOpen; }
            protected set { _isOpen = value; }
        }

        protected virtual void ExtractWapToPath(string path) {
            /*
             * This method is not as effecient as it could be.
             * It takes a stream (which could be a filestream)
             * and writes it out to disk, before it extracts
             * the compressed contents. For large files that
             * already exist on disk, this is wasteful.
             * 
             * This was a trade off of developer time vs.
             * computer time. Perhaps in the future this will
             * be revisited.
             */ 
            string tempFilename = Path.GetRandomFileName();
            string zipFilename = Path.Combine(path, tempFilename);
            byte[] buffer = new byte[_bufferSize];

            using (FileStream zipFile = File.Create(zipFilename)) {
                StreamUtils.Copy(_stream, zipFile, buffer);
                zipFile.Close();
            }

            FastZip fz = new FastZip();
            fz.ExtractZip(zipFilename, path, string.Empty);

            File.Delete(zipFilename);
        }

        protected virtual void DeleteDirectory(string path) {
            string[] files = Directory.GetFiles(path);
            foreach(string file in files) {
                File.Delete(file);
            }

            string[] subDirs = Directory.GetDirectories(path);
            foreach(string subDir in subDirs) {
                DeleteDirectory(subDir);
            }
            Directory.Delete(path);
        }
    }
}
