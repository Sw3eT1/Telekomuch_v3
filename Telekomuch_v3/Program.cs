using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

class HuffmanNode : IComparable<HuffmanNode>
{
    public char? Symbol;
    public int Frequency;
    public HuffmanNode Left, Right;
    public bool IsLeaf => Symbol != null;

    public int CompareTo(HuffmanNode other) => Frequency - other.Frequency;
}

class HuffmanCoding
{
    private Dictionary<char, string> _codes = new();
    private HuffmanNode _root;

    public Dictionary<char, string> Build(string text)
    {
        var freq = text.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        var nodes = new PriorityQueue<HuffmanNode, int>();

        foreach (var kv in freq)
            nodes.Enqueue(new HuffmanNode { Symbol = kv.Key, Frequency = kv.Value }, kv.Value);

        while (nodes.Count > 1)
        {
            var left = nodes.Dequeue();
            var right = nodes.Dequeue();

            var parent = new HuffmanNode
            {
                Frequency = left.Frequency + right.Frequency,
                Left = left,
                Right = right
            };
            nodes.Enqueue(parent, parent.Frequency);
        }

        _root = nodes.Dequeue();
        GenerateCodes(_root, "");
        return _codes;
    }

    private void GenerateCodes(HuffmanNode node, string code)
    {
        if (node == null) return;
        if (node.IsLeaf)
            _codes[node.Symbol!.Value] = code;
        GenerateCodes(node.Left, code + "0");
        GenerateCodes(node.Right, code + "1");
    }

    public byte[] Encode(string text, out Dictionary<char, string> codebook, out int bitLength)
    {
        var bits = new List<bool>();
        foreach (var c in text)
            bits.AddRange(_codes[c].Select(b => b == '1'));

        bitLength = bits.Count;
        codebook = _codes;

        var bytes = new List<byte>();
        for (int i = 0; i < bits.Count; i += 8)
        {
            byte b = 0;
            for (int j = 0; j < 8 && i + j < bits.Count; j++)
            {
                if (bits[i + j]) b |= (byte)(1 << (7 - j));
            }
            bytes.Add(b);
        }
        return bytes.ToArray();
    }

    public string Decode(byte[] data, int bitLength, Dictionary<char, string> codebook)
    {
        // Odtwórz drzewo Huffmana
        _root = BuildTreeFromCodebook(codebook);
        var bits = new List<bool>();

        for (int i = 0; i < data.Length; i++)
        {
            for (int j = 0; j < 8 && bits.Count < bitLength; j++)
            {
                bits.Add((data[i] & (1 << (7 - j))) != 0);
            }
        }

        var result = new StringBuilder();
        var node = _root;
        foreach (var bit in bits)
        {
            node = bit ? node.Right : node.Left;
            if (node.IsLeaf)
            {
                result.Append(node.Symbol);
                node = _root;
            }
        }
        return result.ToString();
    }

    private HuffmanNode BuildTreeFromCodebook(Dictionary<char, string> codebook)
    {
        var root = new HuffmanNode();
        foreach (var kv in codebook)
        {
            var current = root;
            foreach (var bit in kv.Value)
            {
                if (bit == '0')
                {
                    current.Left ??= new HuffmanNode();
                    current = current.Left;
                }
                else
                {
                    current.Right ??= new HuffmanNode();
                    current = current.Right;
                }
            }
            current.Symbol = kv.Key;
        }
        return root;
    }
}

class Program
{
    const int Port = 5000;
    const string Host = "127.0.0.1";
    const string InputFile = "tekst.txt";
    const string OutputFile = "odebrany_tekst.txt";

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Użycie: --server lub --client");
            return;
        }
        if (args[0] == "--server") RunServer();
        else if (args[0] == "--client") RunClient();
        else Console.WriteLine("Nieznana opcja. Użyj --server lub --client");
    }

    static void RunClient()
    {
        string text = File.ReadAllText(InputFile);
        var huffman = new HuffmanCoding();
        var codebook = huffman.Build(text);
        int bitLength;
        var encoded = huffman.Encode(text, out var codes, out bitLength);

        using var client = new TcpClient(Host, Port);
        using var stream = client.GetStream();
        using var writer = new BinaryWriter(stream);

        // Zapisz słownik
        writer.Write(codes.Count);
        foreach (var kv in codes)
        {
            writer.Write((byte)kv.Key);
            writer.Write((byte)kv.Value.Length);
            byte b = 0;
            int bitCount = 0;
            foreach (var bit in kv.Value)
            {
                b <<= 1;
                if (bit == '1') b |= 1;
                bitCount++;
                if (bitCount == 8)
                {
                    writer.Write(b);
                    b = 0;
                    bitCount = 0;
                }
            }
            if (bitCount > 0)
            {
                b <<= (8 - bitCount);
                writer.Write(b);
            }
        }

        // Zapisz długość i zakodowany tekst
        writer.Write(bitLength);
        writer.Write(encoded.Length);
        writer.Write(encoded);

        Console.WriteLine("Wysłano dane do serwera.");
    }

    static void RunServer()
    {
        TcpListener server = new TcpListener(IPAddress.Any, Port);
        server.Start();
        Console.WriteLine("Serwer nasłuchuje...");

        using var client = server.AcceptTcpClient();
        using var stream = client.GetStream();
        using var reader = new BinaryReader(stream);

        int symbolCount = reader.ReadInt32();
        var codebook = new Dictionary<char, string>();

        for (int i = 0; i < symbolCount; i++)
        {
            char symbol = (char)reader.ReadByte();
            int length = reader.ReadByte();
            int byteLen = (length + 7) / 8;
            byte[] bytes = reader.ReadBytes(byteLen);

            string bits = string.Join("", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
            codebook[symbol] = bits.Substring(0, length);
        }

        int bitLength = reader.ReadInt32();
        int byteLength = reader.ReadInt32();
        byte[] encoded = reader.ReadBytes(byteLength);

        var huffman = new HuffmanCoding();
        string decoded = huffman.Decode(encoded, bitLength, codebook);
        File.WriteAllText(OutputFile, decoded);

        Console.WriteLine("Zapisano do pliku: " + OutputFile);
    }
}
