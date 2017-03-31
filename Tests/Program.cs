﻿using System;
using System.Threading;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Diagnostics;
using AppendLog;

namespace Tests
{
    class Program
    {
        static readonly string TXT = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Ut pulvinar mauris commodo, vestibulum leo eget, ullamcorper ante. Nunc finibus nisi malesuada neque condimentum varius. Nullam euismod pulvinar ex, pharetra maximus urna vestibulum in. Morbi nisl tortor, viverra sed arcu in, consectetur vestibulum nibh. Fusce maximus tincidunt viverra. Nunc venenatis posuere nibh, at feugiat turpis aliquet eget. Nullam lacinia leo urna, nec elementum nunc fringilla nec.

Donec aliquet lorem in turpis finibus, non vehicula tortor fermentum. Donec placerat metus ac tincidunt placerat. Sed ut lorem ut magna dignissim convallis sed posuere dolor. Curabitur et ante ac est tristique tempor nec id libero. Suspendisse consequat ullamcorper convallis. Pellentesque non odio molestie, tincidunt sapien sit amet, imperdiet lorem. Aliquam iaculis tortor pretium, ultricies dui sed, efficitur enim. Etiam ut lorem ac risus blandit semper a sit amet dolor. Nunc egestas odio eget arcu semper, sed bibendum metus dapibus. Etiam vel volutpat nisl. Ut in ipsum tellus. Donec posuere purus eu varius sollicitudin.

Nunc libero velit, viverra sit amet ex non, aliquet varius est. Nunc tincidunt ante urna, nec vulputate nunc feugiat id. Etiam rhoncus nibh nibh, et dignissim metus vehicula a. Proin vitae nibh lobortis, pretium ipsum eget, tincidunt enim. Integer sit amet nisi vel augue rhoncus condimentum vel quis mi. Aenean eu ex sit amet nisl pretium condimentum at quis nibh. Vestibulum finibus sollicitudin molestie. Nulla imperdiet malesuada vulputate. Suspendisse vitae fringilla lorem. Duis at mattis sapien, eu interdum nulla. Nullam condimentum nunc dolor, sed consectetur erat laoreet ut.

Sed cursus neque in semper maximus. Integer condimentum erat vel porttitor maximus. Proin augue leo, mollis eu sem sit amet, aliquet vehicula nulla. Suspendisse ex nulla, dictum scelerisque finibus vitae, aliquam a turpis. Aliquam hendrerit, est at convallis lacinia, arcu urna sollicitudin metus, vitae rutrum est lectus at leo. Fusce tincidunt dui eros, vel fringilla ipsum tristique id. Aenean et quam a nunc vestibulum tempus vel at quam. Donec pulvinar bibendum lacinia. Suspendisse in condimentum ex. Praesent eu orci eget lacus accumsan efficitur. Curabitur hendrerit risus mauris, quis tincidunt purus elementum in. Proin eget tristique ipsum. Curabitur eu nisl vel eros consectetur scelerisque eget ut erat. Morbi dapibus condimentum purus, ut pulvinar purus sagittis at. Suspendisse sagittis auctor risus, vel molestie libero sollicitudin sit amet.";

        static void Main(string[] args)
        {
            //BasicTest();
            //MultiThreadTest();
            SingleTestMM();
            SingleTest();
        }

        const int ITER = 9000;
        static FileLog log;
        static byte[] tmpbuf;

        static void SingleTest()
        {
            var clock = new Stopwatch();
            var path = Path.GetFullPath("test.db");
            try
            {
                var buf = new byte[sizeof(long)];
                tmpbuf = Encoding.ASCII.GetBytes(TXT);
                using (var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4 * 4096, false))
                {
                    SingleRun(clock, fs);
                    //using (var file = new BoundedStream(fs, 0, 64 * 1024 * 1024))
                    //{
                    //    SingleRun(clock, file);
                    //}
                }
                PrintStats("SingleFS", clock.ElapsedMilliseconds);
            }
            finally
            {
                File.Delete(path);
            }
        }

        static void SingleTestMM()
        {
            var clock = new Stopwatch();
            var path = Path.GetFullPath("testmm.db");
            try
            {
                var buf = new byte[sizeof(long)];
                tmpbuf = Encoding.ASCII.GetBytes(TXT);
                using (var map = MemoryMappedFile.CreateFromFile(path, FileMode.OpenOrCreate, "testmm", 66 * 1024 * 1024, MemoryMappedFileAccess.ReadWrite))
                {
                    using (var fs = map.CreateViewStream())
                    {
                        SingleRun(clock, fs);
                    }
                    PrintStats("SingleMM", clock.ElapsedMilliseconds);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        static void SingleRun(Stopwatch clock, Stream file)
        {
            var buf = new byte[sizeof(long)];
            // log header
            file.Write(buf, 0, sizeof(int));
            file.Write(buf, 0, sizeof(int));
            file.Write(buf, 0, sizeof(int));
            file.Write(buf, 0, sizeof(int));
            file.Write(buf, 0, sizeof(int));
            file.Write(buf, 0, sizeof(int));

            clock.Start();
            for (int i = 0; i < 3 * ITER; ++i)
            {
                // begin FileLog.Append
                Monitor.Enter(file);
                try
                {
                    var start = file.Position;

                    file.Write(tmpbuf, 0, tmpbuf.Length);
                    // end FileLog.Append

                    // begin FileLog.Append.Dispose
                    var length = file.Position - start;
                    if (length > 0)
                    {
                        //if (file.Position != file.Length)
                        //    file.Seek(0, SeekOrigin.End);
                        buf.Write(length);
                        buf.Write(tmpbuf.Length);
                        file.Write(buf, 0, sizeof(int));
                        file.Flush();
                        var pos = file.Position;
                        file.Seek(16, SeekOrigin.Begin);
                        buf.Write(pos);
                        file.Write(buf, 0, sizeof(long));
                        file.Flush();
                        file.Seek(pos, SeekOrigin.Begin);
                    }
                }
                finally
                {
                    Monitor.Exit(file);
                }
                //end FileLog.Appender.Dispose
            }
            clock.Stop();
            Console.WriteLine("file size: {0} kB", file.Length / 1024);
        }

        static void MultiThreadTest()
        {
            log = FileLog.Create("multi.db", false).Result;
            var clock = new Stopwatch();
            try
            {
                tmpbuf = Encoding.ASCII.GetBytes(TXT);
                clock.Start();
                var t0 = Task.Run(new Action(Run));
                var t1 = Task.Run(new Action(Run));
                Run();
                t0.Wait();
                t1.Wait();
                clock.Stop();
                //log.Stats();
                var count = 0;
                using (var ie = log.Replay(log.First))
                {
                    while (ie.MoveNext().Result)
                    {
                        using (var tr = new StreamReader(ie.Stream))
                        {
                            Debug.Assert(tr.ReadToEnd() == TXT);
                            ++count;
                        }
                    }
                }
                PrintStats("FileLog", clock.ElapsedMilliseconds);
                //Debug.Assert(count == 3 * ITER);
            }
            finally
            {
                log.Dispose();
                File.Delete(Path.GetFullPath("multi.db"));
            }
        }

        static void Run()
        {
            TransactionId tx;
            for (int i = 0; i < ITER; ++i)
            {
                using (var buf = log.Append().Result.Bind(out tx))
                {
                    buf.Write(tmpbuf, 0, tmpbuf.Length);
                }
            }
        }

        static void PrintStats(string name, long ms)
        {
            Console.WriteLine("{0}: {1:0} tx/sec", name, 3000 * ITER / ms);
        }

        static async void BasicTest()
        {
            var path = "basic.db";
            var log = FileLog.Create(path, false).Result;
            try
            {
                var ar0 = await log.Append();
                using (var buf = ar0.Stream)
                {
                    buf.Write(Encoding.ASCII.GetBytes("hello"), 0, 5);
                    buf.Write(Encoding.ASCII.GetBytes("world!"), 0, 6);
                }
                var ar1 = await log.Append();
                using (var buf = ar1.Stream)
                {
                    buf.Write(Encoding.ASCII.GetBytes("hello"), 0, 5);
                    buf.Write(Encoding.ASCII.GetBytes("world!"), 0, 6);
                }
                var count = 0;
                using (var ie = log.Replay(log.First))
                {
                    while (ie.MoveNext().Result)
                    {
                        using (var tr = new StreamReader(ie.Stream))
                        {
                            var tmp = tr.ReadToEnd();
                            Debug.Assert(tmp == "helloworld!");
                            ++count;
                        }
                    }
                }
                Debug.Assert(count == 2);
            }
            finally
            {
                log.Dispose();
                File.Delete(Path.GetFullPath(path));
            }
        }
    }
}
