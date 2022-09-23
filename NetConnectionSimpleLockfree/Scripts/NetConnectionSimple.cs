using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Noemax.GZip;
using UnityEngine;
using UnityEngine.Networking;

namespace NetConnectionSimpleLockfree
{
    public class NetConnectionSimple : NetConnectionAbs
    {
        private struct RecvBuffer
        {
            public RecvBuffer(byte[] _data, int _size)
            {
                Data = _data;
                Size = _size;
            }

            public readonly byte[] Data;
            public readonly int Size;
        }

        private readonly int reservedHeaderBytes;
        private readonly int maxPacketSize;
        private readonly int maxPayloadPerPacket;
        //private readonly object writerListLockObj = new object();
        private volatile bool writeProcessing = false;
        private readonly ManualResetEvent writerTriggerEvent = new ManualResetEvent(false);
        private Queue<NetPackage> queue_write_append = new Queue<NetPackage>();
        private Queue<NetPackage> queue_write_processing = new Queue<NetPackage>();
        private readonly LinkedList<ArrayListMP<byte>> bufsToSend = new LinkedList<ArrayListMP<byte>>();
        private ThreadManager.ThreadInfo writerThreadInfo;

        private readonly byte[] writerBuffer;
        private MemoryStream writerStream;
        private PooledBinaryWriter sendStreamWriter;
        private MemoryStream sendStreamUncompressed;
        private MemoryStream sendStreamCompressed;
        private DeflateOutputStream sendZipStream;

        private Queue<RecvBuffer> queue_read_processing = new Queue<RecvBuffer>();
        private Queue<RecvBuffer> queue_read_append = new Queue<RecvBuffer>();
        private List<NetPackage> list_read_packages_processing = new List<NetPackage>();
        private List<NetPackage> list_read_packages_received = new List<NetPackage>();
        private volatile bool readProcessing = false;
        private readonly ManualResetEvent readerTriggerEvent = new ManualResetEvent(false);
        private ThreadManager.ThreadInfo readerThreadInfo;

        private readonly byte[] readerBuffer;
        private readonly PooledBinaryReader receiveStreamReader = new PooledBinaryReader();
        private MemoryStream receiveStreamCompressed;
        private MemoryStream receiveStreamUncompressed;
        private DeflateInputStream receiveZipStream;

        private const int networkErrorCooldownMs = 500;
        private Coroutine co = null;

        public NetConnectionSimple(int _channel, ClientInfo _clientInfo, INetworkClient _netClient, string _uniqueId, int _reservedHeaderBytes = 0, int _maxPacketSize = 0)
            : base(_channel, _clientInfo, _netClient, _uniqueId)
        {
            reservedHeaderBytes = _reservedHeaderBytes;
            maxPacketSize = _maxPacketSize;
            if (maxPacketSize > 0)
            {
                maxPayloadPerPacket = maxPacketSize - reservedHeaderBytes;
            }
            readerBuffer = new byte[4096];
            writerBuffer = new byte[4096];
            if (_clientInfo != null)
            {
                if (_channel == 0)
                {
                    InitStreams(false);
                }
            }
            else
            {
                InitStreams(true);
            }
            readerThreadInfo = ThreadManager.StartThread("NCS_Reader_" + connectionIdentifier, new ThreadManager.ThreadFunctionDelegate(taskDeserialize), System.Threading.ThreadPriority.Normal, null, null, true);
            writerThreadInfo = ThreadManager.StartThread("NCS_Writer_" + connectionIdentifier, new ThreadManager.ThreadFunctionDelegate(taskSerialize), System.Threading.ThreadPriority.Normal, null, null, true);
            co = ThreadManager.StartCoroutine(CheckRWStateCo());
        }

        protected override void InitStreams(bool _full)
        {
            if (fullConnection)
            {
                return;
            }
            if (_full)
            {
                byte[] array = new byte[2097152];
                receiveStreamCompressed = new MemoryStream(array, 0, array.Length, true, true);
                receiveStreamCompressed.SetLength(0L);
                byte[] array2 = new byte[2097152];
                receiveStreamUncompressed = new MemoryStream(array2, 0, array2.Length, true, true);
                receiveZipStream = new DeflateInputStream(receiveStreamCompressed, true);
                byte[] array3 = new byte[2097152];
                sendStreamUncompressed = new MemoryStream(array3, 0, array3.Length, true, true);
                if (sendStreamWriter == null)
                {
                    sendStreamWriter = new PooledBinaryWriter();
                }
                sendStreamWriter.SetBaseStream(sendStreamUncompressed);
                byte[] array4 = new byte[2097152];
                sendStreamCompressed = new MemoryStream(array4, 0, array4.Length, true, true);
                sendZipStream = new DeflateOutputStream(sendStreamCompressed, 3, true);
                writerStream = new MemoryStream(new byte[2097152]);
                writerStream.SetLength(0L);
                fullConnection = true;
                return;
            }
            byte[] array5 = new byte[32768];
            receiveStreamCompressed = new MemoryStream(array5, 0, array5.Length, true, true);
            receiveStreamCompressed.SetLength(0L);
            byte[] array6 = new byte[32768];
            sendStreamUncompressed = new MemoryStream(array6, 0, array6.Length, true, true);
            sendStreamWriter = new PooledBinaryWriter();
            sendStreamWriter.SetBaseStream(sendStreamUncompressed);
            writerStream = new MemoryStream(new byte[32768]);
            writerStream.SetLength(0L);
        }

        private IEnumerator CheckRWStateCo()
        {
            while(!bDisconnected)
            {
                if(!writeProcessing && queue_write_append.Count > 0)
                {
                    var temp = queue_write_processing;
                    queue_write_processing = queue_write_append;
                    queue_write_append = temp;
                    queue_write_append.Clear();
                    writeProcessing = true;
                    writerTriggerEvent.Set();
                }

                if(!readProcessing)
                {
                    if(list_read_packages_processing.Count > 0)
                    {
                        var temp = list_read_packages_processing;
                        list_read_packages_processing = list_read_packages_received;
                        list_read_packages_received = temp;
                        list_read_packages_processing.Clear();
                    }

                    if(queue_read_append.Count > 0)
                    {
                        var temp = queue_read_processing;
                        queue_read_processing = queue_read_append;
                        queue_read_append = temp;
                        queue_read_append.Clear();
                        readProcessing = true;
                        readerTriggerEvent.Set();
                    }
                }
                yield return null;
            }
            yield break;
        }

        public override void Disconnect()
        {
            base.Disconnect();
            readerTriggerEvent.Set();
            writerTriggerEvent.Set();
            ThreadManager.StopCoroutine(co);
        }

        public override void AddToSendQueue(NetPackage _package)
        {
            _package.RegisterSendQueue();
            //object obj = writerListLockObj;
            //lock (obj)
            //{
            //    writerListFilling.Add(_package);
            //}
            queue_write_append.Enqueue(_package);
        }

        public override void FlushSendQueue()
        {
            //object obj = writerListLockObj;
            //lock (obj)
            //{
            //    writerTriggerEvent.Set();
            //}
            if(!writeProcessing)
            {
                writeProcessing = true;
                writerTriggerEvent.Set();
            }
        }

        public override void AppendToReaderStream(byte[] _data, int _dataSize)
        {
            if (bDisconnected)
            {
                return;
            }
            //Queue<NetConnectionSimple.RecvBuffer> queue = receivedBuffers;
            //lock (queue)
            //{
            //    receivedBuffers.Enqueue(new NetConnectionSimple.RecvBuffer(_data, _dataSize));
            //}
            //readerTriggerEvent.Set();
            queue_read_append.Enqueue(new RecvBuffer(_data, _dataSize));
        }

        public override void GetPackages(List<NetPackage> _dstBuf)
        {
            _dstBuf.Clear();
            _dstBuf.AddRange(list_read_packages_received);
            list_read_packages_received.Clear();
        }

        private void taskDeserialize(ThreadManager.ThreadInfo _threadInfo)
        {
            bool isDataPending = false;
            int dataSize = 0;
            bool isCompressed = false;
            bool isEncrypted = false;
            int packetCount = 0;
            int dataRead = 0;
            try
            {
                while (!bDisconnected && !_threadInfo.TerminationRequested())
                {
                    readerTriggerEvent.WaitOne(8);
                    if (bDisconnected)
                    {
                        break;
                    }
                    Queue<NetConnectionSimple.RecvBuffer> queue = queue_read_processing;
                    NetConnectionSimple.RecvBuffer recvBuffer;
                    //lock (queue)
                    //{
                    //    if (queue_read_processing.Count == 0)
                    //    {
                    //        readerTriggerEvent.Reset();
                    //        continue;
                    //    }
                    //    recvBuffer = queue_read_processing.Dequeue();
                    //}
                    while(queue_read_processing.Count > 0)
                    {
                        recvBuffer = queue_read_processing.Dequeue();
                        int offset = reservedHeaderBytes;
                        if (!isDataPending)
                        {
                            dataRead = 0;
                            dataSize = StreamUtils.ReadInt32(recvBuffer.Data, ref offset);
                            receiveStreamCompressed.Position = 0L;
                            receiveStreamCompressed.SetLength((long)dataSize);
                            isCompressed = StreamUtils.ReadByte(recvBuffer.Data, ref offset) == 1;
                            isEncrypted = StreamUtils.ReadByte(recvBuffer.Data, ref offset) == 1;
                            packetCount = StreamUtils.ReadUInt16(recvBuffer.Data, ref offset);
                            if (packetCount == 0)
                                continue;

                            isDataPending = true;
                        }
                        while (dataRead < dataSize && offset < recvBuffer.Size)
                        {
                            int dataLength = recvBuffer.Size - offset;
                            receiveStreamCompressed.Write(recvBuffer.Data, offset, dataLength);
                            dataRead += dataLength;
                            offset += dataLength;
                        }
                        MemoryPools.poolByte.Free(recvBuffer.Data);
                        if (dataRead >= dataSize)
                        {
                            receiveStreamCompressed.Position = 0L;
                            isDataPending = false;
                            dataRead = 0;
                            stats.RegisterReceivedData(packetCount, dataSize);
                            Decrypt(isEncrypted, receiveStreamCompressed, 0L);
                            receiveStreamReader.SetBaseStream(Decompress(isCompressed, receiveStreamUncompressed, receiveZipStream, readerBuffer) ? receiveStreamUncompressed : receiveStreamCompressed);
                            while (packetCount-- > 0)
                            {
                                int expectedSize = receiveStreamReader.ReadInt32();
                                long position = receiveStreamReader.BaseStream.Position;
                                NetPackage netPackage = NetPackageManager.ParsePackage(receiveStreamReader, cInfo);
                                int realSize = (int)(receiveStreamReader.BaseStream.Position - position);
                                if (realSize != expectedSize)
                                {
                                    string[] array = new string[6];
                                    array[0] = "Parsed data size (";
                                    array[1] = realSize.ToString();
                                    array[2] = ") does not match expected size (";
                                    array[3] = expectedSize.ToString();
                                    array[4] = ") in ";
                                    int num8 = 5;
                                    NetPackage netPackage2 = netPackage;
                                    array[num8] = ((netPackage2 != null) ? netPackage2.ToString() : null);
                                    throw new InvalidDataException(string.Concat(array));
                                }
                                //List<NetPackage> receivedPackages = receivedPackages;
                                //lock (receivedPackages)
                                //{
                                //    receivedPackages.Add(netPackage);
                                //}
                                list_read_packages_processing.Add(netPackage);
                                int packageId = netPackage.PackageId;
                                stats.RegisterReceivedPackage(packageId, realSize);
                                if (ConnectionManager.VerboseNetLogging)
                                {
                                    if (cInfo != null)
                                    {
                                        Log.Out("NCSimple deserialized (cl={3}, ch={0}): {1}, size={2}", new object[]
                                        {
                                        channel,
                                        NetPackageManager.GetPackageName(packageId),
                                        realSize,
                                        cInfo.InternalId.CombinedString
                                        });
                                    }
                                    else
                                    {
                                        Log.Out("NCSimple deserialized (ch={0}): {1}, size={2}", new object[]
                                        {
                                        channel,
                                        NetPackageManager.GetPackageName(packageId),
                                        realSize
                                        });
                                    }
                                }
                            }
                        }
                    }
                    readerTriggerEvent.Reset();
                    readProcessing = false;
                }
            }
            catch (Exception ex)
            {
                if (cInfo != null)
                {
                    Log.Error(string.Format("NCSimple_Deserializer (cl={0}, ch={1}):", cInfo.InternalId.CombinedString, channel));
                }
                else
                {
                    Log.Error(string.Format("NCSimple_Deserializer (ch={0}):", channel));
                }
                Log.Exception(ex);
                Disconnect();
            }
        }

        private void taskSerialize(ThreadManager.ThreadInfo _threadInfo)
        {
            queue_write_processing.Clear();
            bool flag = false;
            bool curQueueHandled = true;
            MicroStopwatch microStopwatch = new MicroStopwatch();
            try
            {
                while (!bDisconnected && !_threadInfo.TerminationRequested())
                {
                    if ((!flag || microStopwatch.ElapsedMilliseconds > 500L) && bufsToSend.Count > 0)
                    {
                        try
                        {
                            flag = !SendBuffers();
                            if (flag)
                            {
                                if (isServer)
                                {
                                    Log.Warning("NET No resources to send data to client ({0}) on channel {2}, backing off for {1} ms", new object[]
                                    {
                                    cInfo.ToString(),
                                    500,
                                    channel
                                    });
                                }
                                else
                                {
                                    Log.Warning("NET No resources to send data to server on channel {1}, backing off for {0} ms", new object[] { 500, channel });
                                }
                                microStopwatch.ResetAndRestart();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Exception(ex);
                            Disconnect();
                        }
                    }
                    if(curQueueHandled)
                        writerTriggerEvent.WaitOne(4);
                    if (bDisconnected)
                    {
                        break;
                    }
                    //object obj = writerListLockObj;
                    //int count;
                    //lock (obj)
                    //{
                    //    count = queue_write_append.Count;
                    //}
                    //if (count == 0)
                    //{
                    //    writerTriggerEvent.Reset();
                    //}
                    //else
                    //{
                    //}
                    //obj = writerListLockObj;
                    //lock (obj)
                    //{
                    //    List<NetPackage> list = queue_write_append;
                    //    List<NetPackage> list2 = queue_write_processing;
                    //    queue_write_processing = list;
                    //    queue_write_append = list2;
                    //    queue_write_append.Clear();
                    //}
                    if (queue_write_processing.Count != 0)
                    {
                        curQueueHandled = false;
                        sendStreamUncompressed.Position = 0L;
                        sendStreamUncompressed.SetLength(0L);
                        PooledBinaryWriter bw = sendStreamWriter;
                        int packetCount = 0;
                        long position = 0;
                        for (int i = 0; i < queue_write_processing.Count; i++)
                        {
                            NetPackage netPackage = queue_write_processing.Peek();
                            if (i > 0 && position + (long)netPackage.GetLength() >= 2097152L)
                            {
                                //obj = writerListLockObj;
                                //lock (obj)
                                //{
                                //    for (int j = queue_write_processing.Count - 1; j >= i; j--)
                                //    {
                                //        queue_write_append.Insert(0, queue_write_processing[j]);
                                //    }
                                //    break;
                                //}
                                break;
                            }
                            queue_write_processing.Dequeue();
                            try
                            {
                                bw.Write(-1);
                                netPackage.write(bw);
                                int dataSize = (int)(bw.BaseStream.Position - position - 4L);
                                bw.BaseStream.Position = position;
                                bw.Write(dataSize);
                                bw.BaseStream.Position = bw.BaseStream.Length;
                                position = bw.BaseStream.Position;
                                packetCount++;
                                int packageId = netPackage.PackageId;
                                stats.RegisterSentPackage(packageId, dataSize);
                                if (ConnectionManager.VerboseNetLogging)
                                {
                                    if (cInfo != null)
                                    {
                                        Log.Out("NCSimple serialized (cl={3}, ch={0}): {1}, size={2}", new object[]
                                        {
                                        channel,
                                        NetPackageManager.GetPackageName(packageId),
                                        dataSize,
                                        cInfo.InternalId.CombinedString
                                        });
                                    }
                                    else
                                    {
                                        Log.Out("NCSimple serialized (ch={0}): {1}, size={2}", new object[]
                                        {
                                        channel,
                                        NetPackageManager.GetPackageName(packageId),
                                        dataSize
                                        });
                                    }
                                }
                                netPackage.SendQueueHandled();
                            }
                            catch (NotSupportedException)
                            {
                                if (packetCount > 0)
                                {
                                    string text;
                                    if (cInfo != null)
                                    {
                                        text = string.Format("(cl={0}, ch={1})", cInfo.InternalId.CombinedString, channel);
                                    }
                                    else
                                    {
                                        text = string.Format("(ch={0})", channel);
                                    }
                                    string[] array = new string[9];
                                    array[0] = "Failed writing ";
                                    array[1] = (i + 1).ToString();
                                    array[2] = ". package to Stream ";
                                    array[3] = text;
                                    array[4] = ": ";
                                    int num3 = 5;
                                    NetPackage netPackage2 = netPackage;
                                    array[num3] = ((netPackage2 != null) ? netPackage2.ToString() : null);
                                    array[6] = " - stream size before: ";
                                    array[7] = position.ToString();
                                    array[8] = ", requeuing. Packages in stream:";
                                    Log.Warning(string.Concat(array));
                                    //int[] array2 = new int[NetPackageManager.KnownPackageCount];
                                    //for (int k = 0; k < i; k++)
                                    //{
                                    //    array2[queue_write_processing[k].PackageId]++;
                                    //}
                                    //for (int l = 0; l < array2.Length; l++)
                                    //{
                                    //    if (array2[l] > 0)
                                    //    {
                                    //        Log.Warning("   " + NetPackageManager.GetPackageName(l) + ": " + array2[l].ToString());
                                    //    }
                                    //}
                                    //obj = writerListLockObj;
                                    //lock (obj)
                                    //{
                                    //    for (int m = queue_write_processing.Count - 1; m >= i; m--)
                                    //    {
                                    //        queue_write_append.Insert(0, queue_write_processing[m]);
                                    //    }
                                    //}
                                    sendStreamUncompressed.SetLength(position);
                                    //queue_write_processing.Clear();
                                }
                                netPackage.SendQueueHandled();
                                continue;
                                throw;
                            }
                            catch (Exception)
                            {
                                netPackage.SendQueueHandled();
                                throw;
                            }
                        }

                        sendStreamUncompressed.Position = 0L;
                        bool flag3 = allowCompression && sendStreamUncompressed.Length > 500L;
                        MemoryStream memoryStream = sendStreamUncompressed;
                        if (Compress(flag3, sendStreamUncompressed, sendZipStream, sendStreamCompressed, writerBuffer, packetCount))
                        {
                            memoryStream = sendStreamCompressed;
                        }
                        bool flag4 = Encrypt(memoryStream, 0L);
                        stats.RegisterSentData(packetCount, (int)memoryStream.Length);
                        StreamUtils.Write(writerStream, (int)memoryStream.Length);
                        StreamUtils.Write(writerStream, flag3 ? 1 : 0);
                        StreamUtils.Write(writerStream, flag4 ? 1 : 0);
                        StreamUtils.Write(writerStream, (ushort)packetCount);
                        if (memoryStream.Length > (long)writerStream.Capacity)
                        {
                            Log.Error(string.Format("Source stream size ({0}) > writer stream capacity ({1}), packages: {2}, compressed: {3}", new object[]
                            {
                            memoryStream.Length,
                            writerStream.Capacity,
                            packetCount,
                            flag3
                            }));
                        }
                        StreamUtils.StreamCopy(memoryStream, writerStream, writerBuffer, true);
                        writerStream.Position = 0L;
                        ArrayListMP<byte> buffer = new ArrayListMP<byte>(MemoryPools.poolByte, (int)writerStream.Length + reservedHeaderBytes);
                        writerStream.Read(buffer.Items, reservedHeaderBytes, (int)writerStream.Length);
                        buffer.Count = (int)(writerStream.Length + (long)reservedHeaderBytes);
                        bufsToSend.AddLast(buffer);
                        writerStream.SetLength(0L);

                        if (queue_write_processing.Count > 0)
                            continue;

                        curQueueHandled = true;
                        writerTriggerEvent.Reset();
                        writeProcessing = false;
                    }
                }
            }
            catch (Exception ex2)
            {
                if (cInfo != null)
                {
                    Log.Error(string.Format("NCSimple_Serializer (cl={0}, ch={1}):", cInfo.InternalId.CombinedString, channel));
                }
                else
                {
                    Log.Error(string.Format("NCSimple_Serializer (ch={0}):", channel));
                }
                Log.Exception(ex2);
                Disconnect();
            }
        }

        private bool SendBuffers()
        {
            NetworkError networkError = NetworkError.Ok;
            while (bufsToSend.Count > 0)
            {
                ArrayListMP<byte> arrayListMP = bufsToSend.First.Value;
                bufsToSend.RemoveFirst();
                if (!bDisconnected)
                {
                    if (maxPacketSize > 0 && arrayListMP.Count > maxPacketSize)
                    {
                        arrayListMP = splitSendBuffer(arrayListMP);
                    }
                    if (isServer)
                    {
                        networkError = cInfo.network.SendData(cInfo, channel, arrayListMP);
                    }
                    else
                    {
                        networkError = netClient.SendData(channel, arrayListMP);
                    }
                }
                if (networkError == NetworkError.NoResources)
                {
                    bufsToSend.AddFirst(arrayListMP);
                    return false;
                }
            }
            return true;
        }

        private ArrayListMP<byte> splitSendBuffer(ArrayListMP<byte> _inBuf)
        {
            int dataSize = _inBuf.Count - reservedHeaderBytes;
            int packetCount = dataSize / maxPayloadPerPacket;
            if (packetCount * maxPayloadPerPacket < dataSize)
            {
                packetCount++;
            }
            for (int i = packetCount - 1; i >= 0; i--)
            {
                int curBufferStartPos = i * maxPayloadPerPacket;
                int packetSize;
                if (i == packetCount - 1)
                {
                    packetSize = dataSize - curBufferStartPos;
                }
                else
                {
                    packetSize = maxPayloadPerPacket;
                }
                ArrayListMP<byte> arrayListMP = new ArrayListMP<byte>(MemoryPools.poolByte, packetSize + reservedHeaderBytes);
                Array.Copy(_inBuf.Items, curBufferStartPos + reservedHeaderBytes, arrayListMP.Items, 1, packetSize);
                arrayListMP.Count = packetSize + reservedHeaderBytes;
                if (i <= 0)
                {
                    return arrayListMP;
                }
                bufsToSend.AddFirst(arrayListMP);
            }
            return null;
        }
    }
}
