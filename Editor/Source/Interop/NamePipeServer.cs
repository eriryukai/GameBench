using System;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace Editor
{
    internal class NamePipeServer
    {
        static void test()
        {
            Console.WriteLine("[C# Server] Starting...");

            // 1. Create the named pipe server stream (Byte mode is best for C++ compatibility)
            using (var server = new NamedPipeServerStream(
                       "MyTestPipe",               // The pipe name
                       PipeDirection.InOut,        // Bidirectional communication
                       1,                          // Max server instances
                       PipeTransmissionMode.Byte)) // Byte transmission mode
            {
                Console.WriteLine("[C# Server] Waiting for C++ client connection...");
                server.WaitForConnection(); // Blocks until a client connects
                Console.WriteLine("[C# Server] Client connected!");

                using (var reader = new StreamReader(server, Encoding.UTF8))
                using (var writer = new StreamWriter(server, Encoding.UTF8) { AutoFlush = true })
                {
                    // 2. Read incoming message from C++
                    string clientMessage = reader.ReadLine();
                    Console.WriteLine($"[C# Server] Received: {clientMessage}");

                    // 3. Send a response back to C++ (add \n so C++ can easily parse the line)
                    string response = "Hello from your C# Server!\n";
                    writer.Write(response);
                    Console.WriteLine("[C# Server] Response sent.");
                }
            }
            Console.WriteLine("[C# Server] Closed.");
        }
    }
}
