using Lucene.Net.Support;
using System;
using System.Threading;
using Debug = Lucene.Net.Diagnostics.Debug; // LUCENENET NOTE: We cannot use System.Diagnostics.Debug because those calls will be optimized out of the release!

namespace Lucene.Net.Util
{
    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements. See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License. You may obtain a copy of the License at
     *
     * http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    using DataInput = Lucene.Net.Store.DataInput;
    using IndexOutput = Lucene.Net.Store.IndexOutput;

    /// <summary>
    /// Intentionally slow <see cref="IndexOutput"/> for testing.
    /// </summary>
    public class ThrottledIndexOutput : IndexOutput
    {
        public const int DEFAULT_MIN_WRITTEN_BYTES = 1024;
        private readonly int bytesPerSecond;
        private IndexOutput @delegate;
        private long flushDelayMillis;
        private long closeDelayMillis;
        private long seekDelayMillis;
        private long pendingBytes;
        private long minBytesWritten;
        private long timeElapsed;
        private readonly byte[] bytes = new byte[1];

        public virtual ThrottledIndexOutput NewFromDelegate(IndexOutput output)
        {
            return new ThrottledIndexOutput(bytesPerSecond, flushDelayMillis, closeDelayMillis, seekDelayMillis, minBytesWritten, output);
        }

        public ThrottledIndexOutput(int bytesPerSecond, long delayInMillis, IndexOutput @delegate)
            : this(bytesPerSecond, delayInMillis, delayInMillis, delayInMillis, DEFAULT_MIN_WRITTEN_BYTES, @delegate)
        {
        }

        public ThrottledIndexOutput(int bytesPerSecond, long delays, int minBytesWritten, IndexOutput @delegate)
            : this(bytesPerSecond, delays, delays, delays, minBytesWritten, @delegate)
        {
        }

        public static int MBitsToBytes(int mbits)
        {
            return mbits * 125000;
        }

        public ThrottledIndexOutput(int bytesPerSecond, long flushDelayMillis, long closeDelayMillis, long seekDelayMillis, long minBytesWritten, IndexOutput @delegate)
        {
            Debug.Assert(bytesPerSecond > 0);
            this.@delegate = @delegate;
            this.bytesPerSecond = bytesPerSecond;
            this.flushDelayMillis = flushDelayMillis;
            this.closeDelayMillis = closeDelayMillis;
            this.seekDelayMillis = seekDelayMillis;
            this.minBytesWritten = minBytesWritten;
        }

        public override void Flush()
        {
            Sleep(flushDelayMillis);
            @delegate.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    Sleep(closeDelayMillis + GetDelay(true));
                }
                finally
                {
                    @delegate.Dispose();
                }
            }
        }

        public override long GetFilePointer()
        {
            return @delegate.GetFilePointer();
        }

        [Obsolete("(4.1) this method will be removed in Lucene 5.0")]
        public override void Seek(long pos)
        {
            Sleep(seekDelayMillis);
            @delegate.Seek(pos);
        }

        public override void WriteByte(byte b)
        {
            bytes[0] = b;
            WriteBytes(bytes, 0, 1);
        }

        public override void WriteBytes(byte[] b, int offset, int length)
        {
            long before = Time.NanoTime();
            // TODO: sometimes, write only half the bytes, then
            // sleep, then 2nd half, then sleep, so we sometimes
            // interrupt having only written not all bytes
            @delegate.WriteBytes(b, offset, length);
            timeElapsed += Time.NanoTime() - before;
            pendingBytes += length;
            Sleep(GetDelay(false));
        }

        protected internal virtual long GetDelay(bool closing)
        {
            if (pendingBytes > 0 && (closing || pendingBytes > minBytesWritten))
            {
                long actualBps = (timeElapsed / pendingBytes) * 1000000000L; // nano to sec
                if (actualBps > bytesPerSecond)
                {
                    long expected = (pendingBytes * 1000L / bytesPerSecond);
                    long delay = expected - (timeElapsed / 1000000L);
                    pendingBytes = 0;
                    timeElapsed = 0;
                    return delay;
                }
            }
            return 0;
        }

        private static void Sleep(long ms)
        {
            if (ms <= 0)
            {
                return;
            }
//#if !NETSTANDARD1_6
//            try
//            {
//#endif 
                Thread.Sleep(TimeSpan.FromMilliseconds(ms));
//#if !NETSTANDARD1_6 // LUCENENET NOTE: Senseless to catch and rethrow the same exception type
//            }
//            catch (ThreadInterruptedException e)
//            {
//                throw new ThreadInterruptedException("Thread Interrupted Exception", e);
//            }
//#endif
        }

        public override long Length
        {
            set
            {
                @delegate.Length = value;
            }

            get
            {
                return @delegate.Length;
            }
        }

        public override void CopyBytes(DataInput input, long numBytes)
        {
            @delegate.CopyBytes(input, numBytes);
        }

        public override long Checksum
        {
            get
            {
                return @delegate.Checksum;
            }
        }
    }
}