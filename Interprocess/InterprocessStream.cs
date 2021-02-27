using DiegoG.Utilities.Collections;
using DiegoG.Utilities.IO;
using MessagePack;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Interprocess
{
    public static class InterprocessStream
    {
        public static bool IsInit { get; private set; } = false;
        private static Mutex StreamMutex { get; set; }
        private static string StreamMutexName { get; } = "DiegoG.ServerManager.InterprocessStream.Mutex";
        public static string DaemonIPCStreamName { get; } = "DiegoG.ServerManager.InterprocessStream.Pipe";
        private static TimeSpan TimeoutTime { get; } = TimeSpan.FromSeconds(5);
        public static class Daemon
        {
            public static ConcurrentQueue<Message> Messages { get; private set; }
            public static TimeSpan CheckInboxInterval { get; set; } = TimeSpan.FromMilliseconds(500);
            public static Thread CheckInboxThread { get; private set; }
            private static NamedPipeServerStream IPCStream { get; set; }
            private static bool CancelInboxRead { get; set; } = false;
            public static void Init()
            {
                if (IsInit)
                    throw new InvalidOperationException("Cannot initialize twice");
                StreamMutex = new Mutex(true, StreamMutexName, out bool createdNew);
                if(!createdNew)
                {
                    StreamMutex.Dispose();
                    throw new InvalidOperationException("Cannot create a new Mutex for this application. A Daemon might already be running");
                }
                StreamMutex.ReleaseMutex();
                IPCStream = new(DaemonIPCStreamName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                Messages = new();
                CancelInboxRead = false;
                CheckInboxThread = new(Read);
                CheckInboxThread.Start();
                IsInit = true;
            }
            public static void Terminate()
            {
                if(!IsInit)
                    throw new InvalidOperationException("Cannot terminate if not initialized");
                StreamMutex.Dispose();
                IPCStream.Dispose();
                CancelInboxRead = true;
                CheckInboxThread = null;
                StreamMutex = null;
                IPCStream = null;
                Messages = null;
                IsInit = false;
            }

            public static void WriteResponse(string[] message)
            {
                Message msg = new(message) { Sender = DaemonIPCStreamName, Type = Message.MessageType.Response };
                WriteStream(msg, IPCStream);
            }

            private static void Read()
            {
                while (!CancelInboxRead)
                {
                    Messages.Enqueue(ReadStream(IPCStream));
                    Thread.Sleep(CheckInboxInterval);
                }
                CancelInboxRead = false;
                throw new OperationCanceledException("CheckInbox Thread was canceled.");
            }
        }
        
        public static class Client
        {
            private static string PipeName { get; set; }
            private static NamedPipeClientStream IPCStream { get; set; }
            //Appends a message to the end of the file going by the index, overwrites the current edited status (to true, regardless of previous status)
            //And increases the count by one, for each message sent.
            public static void Init(string pipeName = ".")
            {
                if (IsInit)
                    throw new InvalidOperationException("Cannot initialize twice");
                StreamMutex = new Mutex(false, StreamMutexName, out bool createdNew);
                if (createdNew)
                {
                    StreamMutex.Dispose();
                    throw new InvalidOperationException("StreamMutex has not been created before, Daemon is not running");
                }
                IPCStream = new(DaemonIPCStreamName, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                PipeName = pipeName;

                StreamMutex.ReleaseMutex();
                IsInit = true;
            }

            public static void Write(string[] message, bool immediate)
            {
                Message msg = new(message) { Sender = PipeName, Type = immediate ? Message.MessageType.Immediate : Message.MessageType.Regular };
                StreamMutex.WaitOne();
                try { IPCStream.Connect((int)TimeSpan.FromSeconds(5).TotalMilliseconds); }
                catch (TimeoutException) { Console.WriteLine("Pipe Connection timed out."); throw; }

                WriteStream(msg with { Sender = PipeName }, IPCStream);
            }

            public static Message WriteRequest(string[] message, bool requestCommand = false)
            {
                Message msg = new Message(message) { Sender = PipeName, Type = requestCommand ? Message.MessageType.RequestCommand : Message.MessageType.Request };

                StreamMutex.WaitOne();
                try { IPCStream.Connect((int)TimeSpan.FromSeconds(5).TotalMilliseconds); }
                catch (TimeoutException) { Console.WriteLine("Pipe Connection timed out."); throw; }

                WriteStream(msg, IPCStream);
                var response = ReadStream(IPCStream, false);
                return response.Type == Message.MessageType.Response ? response : throw new Exception($"Invalid Response Received. Message is not a response. {response}");
            }

            public static void Terminate()
            {
                if (!IsInit)
                    throw new InvalidOperationException("Cannot terminate if not initialized");
                StreamMutex.Dispose();
                IPCStream.Dispose();
                IPCStream = null;
                StreamMutex = null;
                IsInit = false;
            }
        }

        private static void WriteStream(Message msg, PipeStream IPCStream)
        {
            var writedat = Serialization.Serialize.MsgPk(msg);
            byte[] buffer = new byte[writedat.Length + 4];
            writedat.CopyTo(buffer, 4);
            BinaryPrimitives.WriteInt32BigEndian(buffer, writedat.Length);

            if (!StreamMutex.WaitOne(TimeoutTime))
                throw new TimeoutException("Exhausted Wait Time for Mutex");
            IPCStream.Write(buffer);
            StreamMutex.ReleaseMutex();
        }

        private static Message ReadStream(PipeStream IPCStream, bool mutex = true)
        {
            byte[] buffer;
            byte[] length = new byte[4];

            if(mutex)
                StreamMutex.WaitOne();
            IPCStream.Read(length, 0, 4);
            if(mutex)
                StreamMutex.ReleaseMutex();

            buffer = new byte[BinaryPrimitives.ReadInt32BigEndian(length)];

            if (mutex)
                StreamMutex.WaitOne();
            IPCStream.Read(buffer, 0, buffer.Length);
            if (mutex)
                StreamMutex.ReleaseMutex();
            return Serialization.Deserialize<Message>.MsgPk(buffer);
        }

        [MessagePackObject]
        public record Message
        {
            public enum MessageType : byte
            { 
                /// <summary>
                /// Represents a regular message, passed to the Daemon
                /// </summary>
                Regular,
                /// <summary>
                /// Represents a message that should be processed by the Daemon immediately.
                /// </summary>
                Immediate,
                /// <summary>
                /// Represents a message that should be processed by the Daemon immediately, and a response is expected.
                /// </summary>
                Request,
                /// <summary>
                /// Represents a message that should be processed by the Daemon, and a response is expected through a request at some other time. An immediate response pertaining the failure or success of the operation is also expected.
                /// </summary>
                BufferedRequest,
                /// <summary>
                /// Represents a message that should be processed by the Daemon immediately, and a response in the form of a command is expected
                /// </summary>
                RequestCommand,
                /// <summary>
                /// Represents a message that was sent by the Daemon as a response to a Request
                /// </summary>
                Response 
            }
            public static readonly uint AverageSize = 128; //bytes
            public static readonly uint ExpectedMessageCount = 50;
            public static readonly uint ExtraBytes = 4;
            public static uint BufferSize => (AverageSize + ExtraBytes) * ExpectedMessageCount + ExtraBytes;

            [Key(0)]
            public MessageType Type { get; init; }

            /// <summary>
            /// Defines the version of the message. Only change this if a new revision of this class is NOT compatible with a previous one
            /// </summary>
            [Key(1)]
            public ulong Version { get; private init; } = 0;

            [Key(2)]
            public string[] Content { get; init; }

            [Key(3)]
            public string Sender { get; init; }

            public override string ToString()
                => $"[Type: {Enum.GetName(Type)}; Version: {Version}; [Content: {Content.Flatten()}]]";

            Message() => Type = MessageType.Regular;
            internal Message(string[] content) : this() => Content = content;
        }
    }
}
