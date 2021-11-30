using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RangeWithChannel {
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("[主執行緒]啟動");

            var channel = new GoChannel<int>();
            Console.WriteLine("[channel]建立為Unbuffered Channel");

            Task.Run(() => {
                Console.WriteLine("[Goroutine執行緒]啟動");

                Console.WriteLine("[數值寫入10次]啟動(阻塞開始)");
                for (var count = 1; count <= 10; count++) {
                    channel.Input = count;
                    Console.WriteLine($"[數值]{count}寫入[channel]");

                    SpinWait.SpinUntil(() => false, TimeSpan.FromSeconds(1));
                }
                Console.WriteLine("[數值寫入10次]完成(阻塞結束)");

                channel.Close();
                Console.WriteLine("[channel]關閉");

                Console.WriteLine("[Goroutine執行緒]完成");
            });

            Console.WriteLine("從Range屬性取出[channel]結果啟動");
            foreach (var item in channel.Range) {
                Console.WriteLine($"取出<-{item}");
            }
            Console.WriteLine("從Range屬性取出[channel]結果完成");

            Console.WriteLine("[主執行緒]完成");
        }
    }

    public class GoChannel<T> {
        private readonly int? capacity;
        private readonly Channel<T> channel;
        private readonly SpinWait spinWait;
        public T Input {
            set {
                WriteAsync(this.channel, value).AsTask().GetAwaiter().GetResult();
            }
        }
        public T Output {
            get {
                return ReadAsync(this.channel).AsTask().GetAwaiter().GetResult();
            }
        }
        public IEnumerable<T> Range {
            get {
                this.spinWait.Reset();

                while (true) {
                    T result;

                    try {
                        result = this.Output;
                    }
                    catch (ChannelClosedException) {
                        yield break;
                    }

                    yield return result;

                    //自旋等待，降低CPU使用率
                    this.spinWait.SpinOnce();
                }
            }
        }

        public GoChannel(int? capacity = null) {
            this.capacity = capacity;
            this.spinWait = new SpinWait();
            this.channel = capacity != null ? /*Buffered Channel*/ Channel.CreateBounded<T>((int)capacity) : /*Unbuffered Channel*/ Channel.CreateUnbounded<T>();
        }

        public async ValueTask<T> ReadAsync(Channel<T> channel) {
            this.spinWait.Reset();

            while (true) {
                if (!await channel.Reader.WaitToReadAsync().ConfigureAwait(false)) {
                    throw new ChannelClosedException();
                }

                if (channel.Reader.TryRead(out var item)) {
                    return item;
                }

                //自旋等待，降低CPU使用率
                this.spinWait.SpinOnce();
            }
        }

        public async ValueTask WriteAsync(Channel<T> channel, T value) {
            this.spinWait.Reset();

            while (true) {
                if (!await channel.Writer.WaitToWriteAsync().ConfigureAwait(false)) {
                    throw new ChannelClosedException();
                }

                if (channel.Writer.TryWrite(value)) {
                    //Buffered Channel
                    if (this.capacity != null) {
                        return;
                    }
                    //Unbuffered Channel
                    else {
                        //實現推入後尚未拉取的等待
                        while (true) {
                            if (channel.Reader.Count == 0) {
                                return;
                            }

                            //自旋等待，降低CPU使用率
                            this.spinWait.SpinOnce();
                        }
                    }
                }

                //自旋等待，降低CPU使用率
                this.spinWait.SpinOnce();
            }
        }

        public void Close() {
            this.channel.Writer.Complete();
        }
    }
}
