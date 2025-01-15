using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        string serverAddress = "127.0.0.1";
        int serverPort = 3000;

        try
        {
            using (TcpClient client = new TcpClient(serverAddress, serverPort))
            using (NetworkStream networkStream = client.GetStream())
            {
                // Send request to stream all packets
                byte[] requestMessage = { 1, 0 }; // callType = 1
                networkStream.Write(requestMessage, 0, requestMessage.Length);

                // Receive and process responses
                List<Packet> packetList = new List<Packet>();
                byte[] dataBuffer = new byte[1024];
                int receivedBytes;

                while ((receivedBytes = networkStream.Read(dataBuffer, 0, dataBuffer.Length)) > 0)
                {
                    packetList.AddRange(ExtractPackets(dataBuffer, receivedBytes));
                }

                // Identify and handle missing packets
                packetList.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
                int highestSequence = packetList[^1].Sequence;
                for (int seq = 1; seq <= highestSequence; seq++)
                {
                    if (!packetList.Exists(packet => packet.Sequence == seq))
                    {
                        // Request missing packet
                        byte[] missingPacketRequest = { 2, (byte)seq }; // callType = 2
                        networkStream.Write(missingPacketRequest, 0, missingPacketRequest.Length);

                        receivedBytes = networkStream.Read(dataBuffer, 0, dataBuffer.Length);
                        packetList.AddRange(ExtractPackets(dataBuffer, receivedBytes));
                    }
                }

                // Save packets to a JSON file
                string jsonResult = JsonConvert.SerializeObject(packetList, Formatting.Indented);
                File.WriteAllText("output.json", jsonResult);
                Console.WriteLine("Packets successfully written to output.json");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
        }
    }

    static List<Packet> ExtractPackets(byte[] rawData, int length)
    {
        List<Packet> packetCollection = new List<Packet>();
        int offset = 0;

        while (offset < length)
        {
            string stockSymbol = Encoding.ASCII.GetString(rawData, offset, 4);
            offset += 4;
            char transactionType = (char)rawData[offset];
            offset += 1;
            int quantity = BitConverter.ToInt32(rawData, offset);
            offset += 4;
            int unitPrice = BitConverter.ToInt32(rawData, offset);
            offset += 4;
            int sequenceNumber = BitConverter.ToInt32(rawData, offset);
            offset += 4;

            packetCollection.Add(new Packet
            {
                Symbol = stockSymbol,
                BuySellIndicator = transactionType,
                Quantity = quantity,
                Price = unitPrice,
                Sequence = sequenceNumber
            });
        }

        return packetCollection;
    }
}
