using System.Collections;
using System.Text;

namespace Compression;

internal class HuffmanNode
{
    public char? Character { get; set; }
    public int Frequency { get; set; }
    public HuffmanNode? Left { get; set; }
    public HuffmanNode? Right { get; set; }

    public static byte[] Serialize(HuffmanNode root)
    {
        var queue = new Queue<HuffmanNode>();
        queue.Enqueue(root);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();

                writer.Write(node.Character.HasValue);

                if (node.Character.HasValue)
                {
                    writer.Write(node.Character.Value);
                }
                else
                {
                    if (node.Right != null)
                        queue.Enqueue(node.Right);
                    if (node.Left != null)
                        queue.Enqueue(node.Left);
                }
            }
        }
        return stream.ToArray();
    }

    public static HuffmanNode Deserialize(byte[] serializedTree)
    {
        using var memoryStream = new MemoryStream(serializedTree);
        using var reader = new BinaryReader(memoryStream);

        var isLeaf = reader.ReadBoolean();

        if (isLeaf)
        {
            return new HuffmanNode { Character = reader.ReadChar() };
        }

        var root = new HuffmanNode();
        var queue = new Queue<HuffmanNode>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            isLeaf = reader.ReadBoolean();
            if (isLeaf)
            {
                current.Right = new HuffmanNode { Character = reader.ReadChar() };
            }
            else
            {
                current.Right = new HuffmanNode();
                queue.Enqueue(current.Right);
            }

            isLeaf = reader.ReadBoolean();
            if (isLeaf)
            {
                current.Left = new HuffmanNode { Character = reader.ReadChar() };
            }
            else
            {
                current.Left = new HuffmanNode();
                queue.Enqueue(current.Left);
            }
        }

        return root;
    }
}

internal class NodeTraversalInfo
{
    public HuffmanNode Node { get; set; } = null!;
    public List<bool> Path { get; set; } = null!;
}

internal static class Huffman
{
    #region Compressing

    public static void Compress(string srcFile, string dstFile)
    {
        var frequencyMap = new Dictionary<char, int>();

        using (var reader = new StreamReader(srcFile))
        {
            while (reader.Peek() >= 0)
            {
                var character = (char)reader.Read();

                if (!frequencyMap.TryAdd(character, 1))
                {
                    frequencyMap[character]++;
                }
            }
        }

        var root = BuildHuffmanTree(frequencyMap);
        var encodingDictionary = GenerateEncoding(root);

        var totalBits = 0;

        using (var reader = new StreamReader(srcFile))
        {
            while (reader.Peek() > -1)
            {
                if (encodingDictionary.TryGetValue((char)reader.Read(), out var code))
                {
                    totalBits += code.Length;
                }
            }
        }

        var bits = new BitArray(totalBits);
        var currentIndex = 0;

        using (var reader = new StreamReader(srcFile))
        {
            while (reader.Peek() > -1)
            {
                if (!encodingDictionary.TryGetValue((char)reader.Read(), out var code))
                    continue;

                for (var i = 0; i < code.Length; i++)
                {
                    bits[currentIndex] = code[i];
                    currentIndex++;
                }
            }
        }

        WriteToFile(dstFile, HuffmanNode.Serialize(root), bits);
    }

    private static HuffmanNode BuildHuffmanTree(Dictionary<char, int> frequencyMap)
    {
        var priorityQueue = new PriorityQueue<HuffmanNode, int>();

        foreach (var kvp in frequencyMap)
        {
            var node = new HuffmanNode
            {
                Character = kvp.Key,
                Frequency = kvp.Value
            };
            priorityQueue.Enqueue(node, node.Frequency);
        }

        while (priorityQueue.Count > 1)
        {
            var left = priorityQueue.Dequeue();
            var right = priorityQueue.Dequeue();

            var parent = new HuffmanNode()
            {
                Frequency = left.Frequency + right.Frequency,
                Left = left,
                Right = right
            };

            priorityQueue.Enqueue(parent, parent.Frequency);
        }

        return priorityQueue.Dequeue();
    }

    private static Dictionary<char, BitArray> GenerateEncoding(HuffmanNode root)
    {
        var encodingMap = new Dictionary<char, BitArray>();
        var queue = new Queue<NodeTraversalInfo>();

        queue.Enqueue(new NodeTraversalInfo { Node = root, Path = [] });

        while (queue.Count > 0)
        {
            var nodeInfo = queue.Dequeue();
            var node = nodeInfo.Node;

            if (node.Character.HasValue)
            {
                var bits = new BitArray(nodeInfo.Path.ToArray());
                encodingMap.Add(node.Character.Value, bits);
                continue;
            }

            if (node.Left != null)
            {
                var leftPath = new List<bool>(nodeInfo.Path) { false };
                queue.Enqueue(new NodeTraversalInfo { Node = node.Left, Path = leftPath });
            }

            if (node.Right != null)
            {
                var rightPath = new List<bool>(nodeInfo.Path) { true };
                queue.Enqueue(new NodeTraversalInfo { Node = node.Right, Path = rightPath });
            }
        }

        return encodingMap;
    }

    public static void WriteToFile(string fileName, byte[] serializedTree, BitArray compressedText)
    {
        var bytes = new byte[(compressedText.Length + 7) / 8];
        compressedText.CopyTo(bytes, 0);

        using var stream = new FileStream(fileName, FileMode.Create);
        using var writer = new BinaryWriter(stream);

        writer.Write(serializedTree.Length);
        writer.Write(serializedTree);

        writer.Write(compressedText.Length);
        writer.Write(bytes);
    }

    #endregion

    #region Decompressing

    public static void Decompress(string srcFile, string dstFile)
    {
        var (root, compressedText) = ReadFromFile(srcFile);

        var stringBuilder = new StringBuilder();
        var walker = root;

        for (var i = 0; i < compressedText.Length; i++)
        {
            walker = compressedText[i] ? walker?.Right : walker?.Left;

            if (walker != null && walker.Character.HasValue)
            {
                stringBuilder.Append(walker.Character.Value);
                walker = root;
            }
        }

        File.WriteAllText(dstFile, stringBuilder.ToString());
    }

    public static (HuffmanNode tree, BitArray compressedText) ReadFromFile(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open);
        using var reader = new BinaryReader(stream);

        var treeSize = reader.ReadInt32();
        var serializedTree = reader.ReadBytes(treeSize);

        var contentBitSize = reader.ReadInt32();
        var contentByteSize = (contentBitSize + 7) / 8;
        var serializedContent = reader.ReadBytes(contentByteSize);

        var content = new BitArray(serializedContent);
        if (content.Length != contentBitSize)
        {
            var bits = new BitArray(contentBitSize);

            for (var i = 0; i < contentBitSize; i++)
            {
                bits[i] = content[i];
            }

            return (HuffmanNode.Deserialize(serializedTree), bits);
        }

        return (HuffmanNode.Deserialize(serializedTree), content);
    }

    #endregion
}