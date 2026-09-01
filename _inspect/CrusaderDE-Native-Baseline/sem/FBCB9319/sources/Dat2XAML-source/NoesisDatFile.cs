using System;
using System.Text;

namespace SHCDESE.Dat2XAML;

public class NoesisDatFile
{
    public string ClassName { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;

    public NoesisDatFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        byte[] data = File.ReadAllBytes(filePath);
        ParseHeuristic(data);
    }

    public NoesisDatFile(byte[] data)
    {
        ParseHeuristic(data);
    }

    private void ParseHeuristic(byte[] data)
    {
        int cursor = 0;

        // --- Find Class Name ---
        // Logic: Find first "proper string" (~4 chars) and extract UNTIL 0x00 or Control char (like Space 0x20)
        cursor = FindFirstTextSequence(data, cursor, 4);

        if (cursor != -1)
        {
            int start = cursor;
            int length = 0;

            // Read until we hit a terminator (0x00, Space, or "numeric"/control byte)
            while (cursor < data.Length)
            {
                byte b = data[cursor];
                // Stop at 0x00, Space (0x20), or Control characters (< 0x20)
                if (b == 0x00 || b <= 0x20)
                    break;

                cursor++;
                length++;
            }

            ClassName = Encoding.ASCII.GetString(data, start, length);
        }
        else
        {
            ClassName = "Unknown";
        }

        // --- Find Path ---
        // Logic: Find next text sequence, extract until ".xaml" is reached.
        cursor = FindFirstTextSequence(data, cursor, 4);

        if (cursor != -1)
        {
            int start = cursor;
            int end = -1;

            // Look ahead for ".xaml"
            while (cursor < data.Length)
            {
                // Check if we found the suffix ".xaml" (hex: 2E 78 61 6D 6C)
                if (IsSequenceAt(data, cursor, ".xaml"))
                {
                    end = cursor + 5; // Include .xaml
                    break;
                }
                cursor++;
            }

            if (end != -1)
            {
                Path = Encoding.ASCII.GetString(data, start, end - start);
                cursor = end; // Move cursor after the path
            }
        }

        // --- Find Content (XML) ---
        // Logic: Find first "<" (representing <Element/ResourceDictionary), continue until last ">" (/Element>)

        // Scan forward for first '<'
        int xmlStart = -1;
        for (int i = cursor; i < data.Length; i++)
        {
            if (data[i] == (byte)'<')
            {
                xmlStart = i;
                break;
            }
        }

        // A few assets append a binary dependency table after the XAML. One
        // asset also has two stray NUL bytes immediately after its first '<'.
        // Remove only those leading NULs, then stop at the first later NUL so
        // dependency bytes can never become part of the XML document.
        var xmlBytes = new List<byte>();
        if (xmlStart != -1)
        {
            int significantBytes = 0;
            for (int i = xmlStart; i < data.Length; i++)
            {
                byte value = data[i];
                if (value == 0)
                {
                    if (significantBytes < 16)
                        continue;
                    break;
                }
                xmlBytes.Add(value);
                if (value > 0x20)
                    significantBytes++;
            }

            // OST_Pings contains an isolated '<' before the real document.
            // Discard such a prefix only when the next non-whitespace byte is
            // itself the start of an XML tag.
            int nextTag = 1;
            while (nextTag < xmlBytes.Count && xmlBytes[nextTag] <= 0x20)
                nextTag++;
            if (xmlBytes.Count > 0 && xmlBytes[0] == (byte)'<' &&
                nextTag < xmlBytes.Count && xmlBytes[nextTag] == (byte)'<')
                xmlBytes.RemoveRange(0, nextTag);
        }

        // Scan backward within the isolated XML payload for its last '>'.
        int xmlEnd = -1;
        for (int i = xmlBytes.Count - 1; i >= 0; i--)
        {
            if (xmlBytes[i] == (byte)'>')
            {
                xmlEnd = i + 1; // Include the '>'
                break;
            }
        }

        if (xmlStart != -1 && xmlEnd > 0)
        {
            Content = Encoding.ASCII.GetString(xmlBytes.ToArray(), 0, xmlEnd);
        }
        else
        {
            Content = string.Empty;
        }
    }

    // --- Helpers ---

    /// <summary>
    /// Scans the byte array starting at 'offset' for the first sequence of valid text characters 
    /// that is at least 'minLength' long.
    /// </summary>
    private int FindFirstTextSequence(byte[] data, int offset, int minLength)
    {
        for (int i = offset; i <= data.Length - minLength; i++)
        {
            if (IsValidTextChar(data[i]))
            {
                // Check if the next (minLength-1) chars are also valid
                bool match = true;
                for (int j = 1; j < minLength; j++)
                {
                    if (!IsValidTextChar(data[i + j]))
                    {
                        match = false;
                        i += j; // Skip ahead
                        break;
                    }
                }

                if (match) return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Defines what a "valid" character is for the start of a string (Letters, dots, underscores).
    /// </summary>
    private bool IsValidTextChar(byte b)
    {
        // A-Z, a-z, ., _
        return (b >= 'A' && b <= 'Z') ||
               (b >= 'a' && b <= 'z') ||
               b == '.' ||
               b == '_';
    }

    /// <summary>
    /// Checks if the ASCII bytes for string 'seq' exist at data[index].
    /// </summary>
    private bool IsSequenceAt(byte[] data, int index, string seq)
    {
        if (index + seq.Length > data.Length) return false;

        for (int i = 0; i < seq.Length; i++)
        {
            // Case-insensitive check just in case, or remove ToLower for strict
            char fileChar = (char)data[index + i];
            if (char.ToLowerInvariant(fileChar) != char.ToLowerInvariant(seq[i]))
                return false;
        }
        return true;
    }
}
