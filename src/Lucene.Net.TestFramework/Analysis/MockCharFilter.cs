using Lucene.Net.Support;
using System.IO;
using Debug = Lucene.Net.Diagnostics.Debug; // LUCENENET NOTE: We cannot use System.Diagnostics.Debug because those calls will be optimized out of the release!

namespace Lucene.Net.Analysis
{
    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements.  See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License.  You may obtain a copy of the License at
     *
     *     http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    /// <summary>
    /// The purpose of this charfilter is to send offsets out of bounds
    /// if the analyzer doesn't use <see cref="CharFilter.CorrectOffset(int)"/> or does incorrect offset math.
    /// </summary>
    public class MockCharFilter : CharFilter
    {
        internal readonly int remainder;

        // for testing only
        public MockCharFilter(TextReader @in, int remainder)
            : base(@in)
        {
            // TODO: instead of fixed remainder... maybe a fixed
            // random seed?
            this.remainder = remainder;
            if (remainder < 0 || remainder >= 10)
            {
                throw new System.ArgumentException("invalid remainder parameter (must be 0..10): " + remainder);
            }
        }

        // for testing only, uses a remainder of 0
        public MockCharFilter(TextReader @in)
            : this(@in, 0)
        {
        }

        internal int currentOffset = -1;
        internal int delta = 0;
        internal int bufferedCh = -1;

        public override int Read()
        {
            // we have a buffered character, add an offset correction and return it
            if (bufferedCh >= 0)
            {
                int ch = bufferedCh;
                bufferedCh = -1;
                currentOffset++;

                AddOffCorrectMap(currentOffset, delta - 1);
                delta--;
                return ch;
            }

            // otherwise actually read one
            int c = m_input.Read();
            if (c < 0)
            {
                return c;
            }

            currentOffset++;
            if ((c % 10) != remainder || char.IsHighSurrogate((char)c) || char.IsLowSurrogate((char)c))
            {
                return c;
            }

            // we will double this character, so buffer it.
            bufferedCh = c;
            return c;
        }

        public override int Read(char[] cbuf, int off, int len)
        {
            // Java returns -1, maintain compat.
            int numRead = 0;
            for (int i = off; i < off + len; i++)
            {
                int c = Read();
                if (c == -1) break;
                cbuf[i] = (char)c;
                numRead++;
            }
            return numRead == 0 ? -1 : numRead;
        }

        protected override int Correct(int currentOff)
        {
            Support.C5.KeyValuePair<int, int> lastEntry;
            int ret;
            // LUCENENET NOTE: TryPredecessor is equivalent to TreeMap.lowerEntry() in Java
            if (corrections.TryPredecessor(currentOff + 1, out lastEntry))
            {
                ret = currentOff + lastEntry.Value;
            }
            else
            {
                ret = currentOff;
            }

            Debug.Assert(ret >= 0, "currentOff=" + currentOff + ",diff=" + (ret - currentOff));
            return ret;
        }

        protected virtual void AddOffCorrectMap(int off, int cumulativeDiff)
        {
            corrections[off] = cumulativeDiff;
        }

        internal TreeDictionary<int, int> corrections = new TreeDictionary<int, int>();
    }
}