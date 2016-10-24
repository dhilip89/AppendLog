﻿using System;
using System.IO;
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
            MultiThreadTest();
            //SingleTest();
        }

        const int ITER = 3000;
        static FileLog log;
        static byte[] tmpbuf;

        static void SingleTest()
        {
            var clock = new Stopwatch();
            var path = Path.GetFullPath("test.db");
            try
            {
                tmpbuf = Encoding.ASCII.GetBytes(TXT);
                using (var fs = File.OpenWrite(path))
                {
                    clock.Start();
                    var file = new BoundedStream(fs, fs.Length, int.MaxValue);
                    for (int i = 0; i < 3 * ITER; ++i)
                    {
                        file.Write(tmpbuf, 0, tmpbuf.Length);
                        file.Write(tmpbuf, 0, sizeof(int));
                        file.Flush();
                        file.Seek(0, SeekOrigin.Begin);
                        file.Write(tmpbuf, 0, sizeof(int));
                        file.Flush();
                        file.Seek(0, SeekOrigin.End);
                    }
                    clock.Stop();
                }
                var secs = clock.ElapsedMilliseconds / 1000.0;
                Console.WriteLine("Single: {0:0} tx/sec", 3 * ITER / secs);
            }
            finally
            {
                File.Delete(path);
            }
        }

        static void MultiThreadTest()
        {
            log = FileLog.Create("multi.db").Result;
            var clock = new Stopwatch();
            try
            {
                tmpbuf = Encoding.ASCII.GetBytes(TXT);
                clock.Start();
                //var t0 = Task.Run(new Action(Run));
                //var t1 = Task.Run(new Action(Run));
                Run();
                //t0.Wait();
                //t1.Wait();
                clock.Stop();
                //var count = 0;
                //using (var ie = log.Replay(log.First))
                //{
                //    while (ie.MoveNext().Result)
                //    {
                //        using (var tr = new StreamReader(ie.Stream))
                //        {
                //            Debug.Assert(tr.ReadToEnd() == TXT);
                //            ++count;
                //        }
                //    }
                //}
                var secs = clock.ElapsedMilliseconds / 1000.0;
                Console.WriteLine("FileLog: {0:0} tx/sec", 3 * ITER / secs);
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
            var tx = log.First;
            for (int i = 0; i < 3 * ITER; ++i)
            {
                Stream output;
                using (log.Append(out output, out tx))
                {
                    output.Write(tmpbuf, 0, tmpbuf.Length);
                }
            }
        }

        static void BasicTest()
        {
            var path = "basic.db";
            var log = FileLog.Create(path).Result;
            try
            {
                TransactionId tx;
                Stream buf;
                using (log.Append(out buf, out tx))
                {
                    buf.Write(Encoding.ASCII.GetBytes("hello"), 0, 5);
                    buf.Write(Encoding.ASCII.GetBytes("world!"), 0, 6);
                }
                using (log.Append(out buf, out tx))
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
