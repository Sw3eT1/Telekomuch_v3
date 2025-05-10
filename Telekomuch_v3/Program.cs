using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

class HuffmanNode
{
    public char? Symbol;
    public int Frequency;
    public HuffmanNode Left;
    public HuffmanNode Right;
}

class HuffmanCoding
{
    private Dictionary<char, string> _codes;
    private HuffmanNode _root;

    public Dictionary<char, string> Build(string text)
    {
        var frequency = text.GroupBy(c => c)
                            .ToDictionary(g => g.Key, g => g.Count());

        var nodes = frequency.Select(f => new HuffmanNode { Symbol = f.Key, Frequency = f.Value }).ToList();

        while (nodes.Count > 1)
        {
            var ordered = nodes.OrderBy(n => n.Frequency).ToList();
            var left = ordered[0];
            var right = ordered[1];

            var parent = new HuffmanNode
            {
                Frequency = left.Frequency + right.Frequency,
                Left = left,
                Right = right
            };

            nodes.Remove(left);
            nodes.Remove(right);
            nodes.Add(parent);
        }

        _root = nodes[0];
        _codes = new Dictionary<char, string>();
        GenerateCodes(_root, "");

        return _codes;
    }

    private void GenerateCodes(HuffmanNode node, string code)
    {
        if (node == null) return;

        if (node.Symbol != null)
            _codes[node.Symbol.Value] = code;

        GenerateCodes(node.Left, code + "0");
        GenerateCodes(node.Right, code + "1");
    }

    public string Encode(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text)
            sb.Append(_codes[c]);
        return sb.ToString();
    }

    public string Decode(string encodedText)
    {
        var result = new StringBuilder();
        var node = _root;

        foreach (var bit in encodedText)
        {
            node = (bit == '0') ? node.Left : node.Right;

            if (node.Symbol != null)
            {
                result.Append(node.Symbol.Value);
                node = _root;
            }
        }

        return result.ToString();
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

        if (args[0] == "--server")
            RunServer();
        else if (args[0] == "--client")
            RunClient();
        else
            Console.WriteLine("Nieznana opcja. Domyslnie uzywana opcja serwer!");
        RunServer();
    }

    static void RunServer()
    {
        TcpListener server = new TcpListener(IPAddress.Any, Port);
        server.Start();
        Console.WriteLine("Serwer nasłuchuje na porcie " + Port);

        using var client = server.AcceptTcpClient();
        Console.WriteLine("Połączono z klientem.");
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string encodedText = reader.ReadLine();
        string originalText = reader.ReadLine();

        File.WriteAllText(OutputFile, originalText);
        Console.WriteLine("Zapisano dane do pliku: " + OutputFile);
    }

    static void RunClient()
    {
        if (!File.Exists(InputFile))
        {
            Console.WriteLine($"Brak pliku {InputFile}");
            return;
        }

        string text = File.ReadAllText(InputFile);
        var huffman = new HuffmanCoding();
        huffman.Build(text);
        string encoded = huffman.Encode(text);

        using var client = new TcpClient(Host, Port);
        using var stream = client.GetStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        writer.WriteLine(encoded);
        writer.WriteLine(text);

        Console.WriteLine("Dane zostały wysłane.");
    }
}
