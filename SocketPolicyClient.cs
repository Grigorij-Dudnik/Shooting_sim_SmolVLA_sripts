using System;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

public class SocketPolicyClient : System.IDisposable
{
    private TcpClient tcpClient;
    private NetworkStream networkStream;
    private string host;
    private int port;

    public SocketPolicyClient(string host, int port)
    {
        this.host = host;
        this.port = port;
        ConnectToServer();
    }

    private void ConnectToServer()
    {
        try
        {
            tcpClient = new TcpClient();
            tcpClient.Connect(host, port);
            networkStream = tcpClient.GetStream();
            Debug.Log($"Socket client connected to {host}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to server: {e.Message}");
            throw;
        }
    }

    public float[] GetAction(byte[] imageData, float[] jointStates, float timestamp)
    {
        try
        {
            // Serialize observation
            byte[] message = SerializeObservation(timestamp, imageData, jointStates);
            
            // Send message with length prefix
            SendMessage(message);

            // Receive response
            byte[] response = ReceiveMessage();
            
            // Parse action
            return ParseAction(response);
        }
        catch (Exception e)
        {
            Debug.LogError($"Socket Error: {e.Message}");
            return new float[4];
        }
    }

    private byte[] SerializeObservation(float timestamp, byte[] imageData, float[] jointStates)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            // Write timestamp
            WriteFloat(writer, timestamp);
            
            // Write image data
            WriteUInt32(writer, (uint)imageData.Length);
            writer.Write(imageData);
            
            // Write joint states
            WriteUInt32(writer, (uint)jointStates.Length);
            foreach (float value in jointStates)
            {
                WriteFloat(writer, value);
            }
            
            return stream.ToArray();
        }
    }

    private float[] ParseAction(byte[] data)
    {
        using (var stream = new MemoryStream(data))
        using (var reader = new BinaryReader(stream))
        {
            uint count = ReadUInt32(reader);
            float[] actions = new float[count];
            
            for (int i = 0; i < count; i++)
            {
                actions[i] = ReadFloat(reader);
            }
            
            return actions;
        }
    }

    private void SendMessage(byte[] data)
    {
        // Send length prefix
        byte[] lengthBytes = GetBigEndianBytes((uint)data.Length);
        networkStream.Write(lengthBytes, 0, 4);
        
        // Send data
        networkStream.Write(data, 0, data.Length);
    }

    private byte[] ReceiveMessage()
    {
        // Receive length
        byte[] lengthBytes = ReceiveExact(4);
        uint length = ParseBigEndianUInt32(lengthBytes);
        
        // Receive data
        return ReceiveExact((int)length);
    }

    private byte[] ReceiveExact(int count)
    {
        byte[] buffer = new byte[count];
        int offset = 0;
        
        while (offset < count)
        {
            int received = networkStream.Read(buffer, offset, count - offset);
            if (received == 0)
                throw new Exception("Server disconnected");
            offset += received;
        }
        
        return buffer;
    }

    private void WriteFloat(BinaryWriter writer, float value)
    {
        writer.Write(GetBigEndianBytes(value));
    }

    private void WriteUInt32(BinaryWriter writer, uint value)
    {
        writer.Write(GetBigEndianBytes(value));
    }

    private float ReadFloat(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    private uint ReadUInt32(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private byte[] GetBigEndianBytes(float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return bytes;
    }

    private byte[] GetBigEndianBytes(uint value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return bytes;
    }

    private uint ParseBigEndianUInt32(byte[] bytes)
    {
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    public void Dispose()
    {
        networkStream?.Close();
        tcpClient?.Close();
    }

    public void Shutdown()
    {
        Dispose();
    }
}