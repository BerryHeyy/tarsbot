using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using DSharpPlus.Entities;

namespace tarsbot.commands
{
    class ProgramEnvironment
    {
        private static readonly ushort MAX_MEM = 0xFFFF;
        private static readonly ushort PC_START_LOC = 0xFFFC;
        private static readonly ushort SP_START_LOC = 0x0100;

        private const byte
            INS_PUS = 0x01,
            INS_ADD = 0x02,
            INS_LDA = 0x41,
            INS_BUP = 0x52,
            INS_SHA = 0x61,
            INS_SDA = 0x62,
            INS_SCA = 0x63,
            INS_PHA = 0x71,
            INS_PLA = 0x81;

        private readonly string programCode;
        private readonly DiscordChannel channel;

        private byte A;

        private byte[] memory = new byte[MAX_MEM];
        private ushort PC = PC_START_LOC;
        private ushort SP = SP_START_LOC;

        // Flags
        private bool V, Z = false;

        public ProgramEnvironment(string program, DiscordChannel channel)
        {
            this.programCode = program;
            this.channel = channel;
        }

        public async Task<bool> Compile(bool dumpMemory, bool fullDump)
        {
            ushort compilePointer = 0x0200;
            string[] lines = this.programCode.Split('\n');

            foreach (string line in lines)
            {
                string[] instructions = line.Split(' ');

                switch (instructions[0].ToUpper())
                {
                    case "LDA":
                        if (instructions.Length != 2) return await ThrowArgumentException(1, instructions.Length - 1, line);

                        memory[compilePointer] = INS_LDA;
                        compilePointer++;

                        try
                        {
                            memory[compilePointer] = Convert.ToByte(instructions[1], 16);
                            compilePointer++;
                        } catch (OverflowException e)
                        {
                            await ThrowPossibleOverflowException(line, "0xFF (Byte)", instructions[1]);
                        } catch (FormatException e)
                        {
                            await ThrowHexStringFormatException(line, instructions[1]);
                        }

                        break;
                    case "ADD":
                        if (instructions.Length != 2) return await ThrowArgumentException(1, instructions.Length - 1, line);

                        memory[compilePointer] = INS_ADD;
                        compilePointer++;

                        try
                        {
                            memory[compilePointer] = Convert.ToByte(instructions[1], 16);
                            compilePointer++;
                        }
                        catch (OverflowException e)
                        {
                            await ThrowPossibleOverflowException(line, "0xFF (Byte)", instructions[1]);
                        }
                        catch (FormatException e)
                        {
                            await ThrowHexStringFormatException(line, instructions[1]);
                        }

                        break;
                    case "BUP":
                        if (instructions.Length != 1) return await ThrowArgumentException(0, instructions.Length - 1, line);

                        memory[compilePointer] = INS_BUP;
                        compilePointer++;

                        break;
                    case "SHA":
                        if (instructions.Length != 1) return await ThrowArgumentException(0, instructions.Length - 1, line);
                        memory[compilePointer] = INS_SHA;
                        compilePointer++;
                        break;
                    case "SDA":
                        if (instructions.Length != 1) return await ThrowArgumentException(0, instructions.Length - 1, line);
                        memory[compilePointer] = INS_SDA;
                        compilePointer++;
                        break;
                    case "SCA":
                        if (instructions.Length != 1) return await ThrowArgumentException(0, instructions.Length - 1, line);
                        memory[compilePointer] = INS_SCA;
                        compilePointer++;
                        break;
                    case "PHA":
                        if (instructions.Length != 1) return await ThrowArgumentException(0, instructions.Length - 1, line);
                        memory[compilePointer] = INS_PHA;
                        compilePointer++;
                        break;
                    case "PLA":
                        if (instructions.Length != 1) return await ThrowArgumentException(0, instructions.Length - 1, line);
                        memory[compilePointer] = INS_PLA;
                        compilePointer++;
                        break;
                    case "PUS":
                        if (instructions.Length != 1) return await ThrowArgumentException(0, instructions.Length - 1, line);
                        memory[compilePointer] = INS_PUS;
                        compilePointer++;
                        break;
                    default:
                        await channel.SendMessageAsync("Exception while compiling. At line: `" + line + "`. OPCODEException: Provided instruction (`" + instructions[0] + "`) not recognised.");
                        return false;
                }
            }

            if (dumpMemory) await DumpMemory(fullDump, false);

            return true;
        }

        public async Task<bool> Run(bool dumpMemory, bool fullDump)
        {

            SP = SP_START_LOC;

            // Default initialization of the power on start memory address to 0x0200
            memory[0xFFFD] = 0x00;
            memory[0xFFFE] = 0x02;

            PC = Convert.ToUInt16(memory[0xFFFE] << 8 | memory[0xFFFD]);

            memory[0xFFFD] = 0x00;
            memory[0xFFFE] = 0x00;

            while (PC < MAX_MEM)
            {
                byte instruction = memory[PC];
                PC++;

                switch (instruction)
                {
                    case INS_LDA:

                        A = memory[PC];
                        PC++;

                        break;
                    case INS_ADD:

                        try
                        {
                            A += memory[PC];                            
                        } catch (OverflowException)
                        {
                            A = byte.MaxValue;
                            V = true;
                        }

                        PC++;
                        break;
                    case INS_BUP:
                        {
                            byte[] bytes = new byte[8];

                            for (int i = 0; i < 8; i++)
                            {
                                bytes[i] = PullStack();
                            }

                            ulong userId = BitConverter.ToUInt64(bytes, 0);

                            DiscordMember member = channel.Guild.GetMemberAsync(userId).Result;

                            await channel.Guild.BanMemberAsync(member);

                        }
                        break;
                    case INS_SHA:
                        await channel.SendMessageAsync(A.ToString("X2"));
                        break;
                    case INS_SDA:
                        await channel.SendMessageAsync(A.ToString());
                        break;
                    case INS_SCA:
                        await channel.SendMessageAsync(Encoding.UTF8.GetString(new[] { A }));
                        break;
                    case INS_PHA:
                        PushStack(A);
                        break;
                    case INS_PLA:
                        A = PullStack();
                        break;
                    case INS_PUS:
                        {
                            byte[] bytes = new byte[8];

                            for (int i = 0; i < 8; i++)
                            {
                                bytes[i] = PullStack();
                            }

                            ulong userId = BitConverter.ToUInt64(bytes, 0);

                            DiscordMember member = channel.Guild.GetMemberAsync(userId).Result;

                            await channel.SendMessageAsync(member.Mention);
                        }
                        break;
                    case 0x00:
                        continue;
                        break;
                }
            }

            if (dumpMemory) await DumpMemory(fullDump, true);

            return true;
        }

        private byte PullStack()
        {
            byte toReturn = memory[SP];
            memory[SP] = 0x00;
            SP--;
            return toReturn;
        }

        private void PushStack(byte toPush)
        {
            SP++;
            memory[SP] = toPush;
        }

        private async Task<bool> ThrowArgumentException(int expected, int provided, string line)
        {
            await channel.SendMessageAsync("Exception while compiling code. At line: `" + line + "`. ArgumentException: Provided " + provided + "arguments. Expected " + expected + "arguments.");
            return false;
        }

        private async Task ThrowPossibleOverflowException(string line, string expected, string provided)
        {
            await channel.SendMessageAsync("Exception while compiling code. At line: `" + line + "`. PossibleOverflowException: Max: `" + expected + "`, provided: `" + provided + "`.");
        }

        private async Task ThrowHexStringFormatException(string line, string provided)
        {
            await channel.SendMessageAsync(String.Format("Exception while compiling code. At line: `{0}`. HexStringFormatException: Provided (`{1}`) hex number is not a valid hex number.", line, provided));
        }

        private async Task DumpMemory(bool fullDump, bool postMemory)
        {

            string fileName = @"D:\Temp\memoryDump.txt";

            try
            {
                if (File.Exists(fileName)) File.Delete(fileName);

                string hexString = BitConverter.ToString(memory);

                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("Offset    00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F\n\n");

                int i = 0;
                ushort memLoc = 0x0000;
                for (ushort j = 0; j < ushort.MaxValue; j++)
                //foreach (byte memByte in memory)
                {
                    if (i == 16)
                    {
                        //stringBuilder.Append("       "); // 7
                        stringBuilder.Append("\n");
                        i = 0;
                        
                    }
                    if (i == 0)
                    {
                        stringBuilder.Append("0x" + memLoc.ToString("X4") + "    ");
                    }


                    stringBuilder.Append(memory[memLoc].ToString("X2") + " ");
                    i++;
                    memLoc++;
                }

                string stringToDump = stringBuilder.ToString();

                // Concat the dump.
                if (!fullDump)
                {

                    

                }


                using (FileStream fs = File.Create(fileName))
                {
                    byte[] charBytes = new UTF8Encoding(true).GetBytes(stringToDump);
                    fs.Write(charBytes, 0, charBytes.Length);
                }

                using (FileStream fs = File.OpenRead(fileName))
                {

                    DiscordMessageBuilder builder = new DiscordMessageBuilder();
                    builder.WithFile(fs);
                    builder.WithContent(postMemory ? "Post-execution State of Memory:" : "Pre-execution State of Memory:");

                    await channel.SendMessageAsync(builder);
                }

            } catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
