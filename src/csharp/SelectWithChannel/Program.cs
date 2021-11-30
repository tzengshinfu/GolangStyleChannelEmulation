using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static SelectWithChannel.GoSelect;
using static SelectWithChannel.GoSelect.ChannelDirection;

namespace SelectWithChannel {
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("[主執行緒]啟動");

            var mainChannel = new GoChannel<int>();
            Console.WriteLine("[mainChannel]建立為Unbuffered Channel");

            var quitChannel = new GoChannel<int>();
            Console.WriteLine("[quitChannel]建立為Unbuffered Channel");

            Task.Run(() => {
                Console.WriteLine("[Goroutine執行緒]啟動");

                Console.WriteLine("[數值]從[mainChannel]取出啟動(阻塞開始)");
                for (var count = 1; count <= 10; count++) {
                    Console.WriteLine($"取出<-{mainChannel.Output}");
                }
                Console.WriteLine("[數值]從[mainChannel]取出完成(阻塞結束)");

                quitChannel.Input = 0;
                Console.WriteLine("[數值]0寫入[quitChannel]");

                Console.WriteLine("[Goroutine執行緒]完成");
            });

            var x = 0;
            Console.WriteLine("x = 0");

            Console.WriteLine("以for迴圈重覆執行GoSelect啟動");
            for (; ; ) {
                Console.WriteLine("進入GoSelect");
                var exitFor = new GoSelect()
                    .Case(mainChannel, Input, x,
                    (_) => {
                        Console.WriteLine($"[數值]{x}寫入[mainChannel]");
                        x += 2;
                        Console.WriteLine("x += 2");
                    })
                    .Case(quitChannel, Output,
                    (_) => {
                        return ExitFor;
                    })
                    .End();
                Console.WriteLine("離開GoSelect");

                if (exitFor) {
                    Console.WriteLine("離開for迴圈");

                    break;
                }

                SpinWait.SpinUntil(() => false, 1); //降低CPU使用率
            }
            Console.WriteLine("以for迴圈重覆執行GoSelect結束");

            Console.WriteLine("[主執行緒]結束");
        }
    }

    public class GoSelect {
        private readonly List<Task<bool?>> tasks = new();
        public readonly static bool ExitFor = true;
        public enum ChannelDirection {
            Input,
            Output
        }

        public GoSelect() { }

        public GoSelect Case<T>(GoChannel<T> channel, ChannelDirection direction, T parameter, Func<T, bool?> func) {
            if (direction != ChannelDirection.Input) {
                throw new Exception(@"[ChannelDirection] must be ""Input""");
            }

            var task = Task.Run(() => {
                channel.Input = parameter;
                var exitFor = func(parameter);

                return exitFor;
            });

            this.tasks.Add(task);

            return this;
        }

        public GoSelect Case<T>(T parameter, GoChannel<T> channel, ChannelDirection direction, Func<T, bool?> func) {
            if (direction != ChannelDirection.Output) {
                throw new Exception(@"[ChannelDirection] must be ""Output""");
            }

            var task = Task.Run(() => {
                parameter = channel.Output;
                var exitFor = func(parameter);

                return exitFor;
            });

            this.tasks.Add(task);

            return this;
        }

        public GoSelect Case<T>(GoChannel<T> channel, ChannelDirection direction, Func<T, bool?> func) {
            if (direction != ChannelDirection.Output) {
                throw new Exception(@"[ChannelDirection] must be ""Output""");
            }

            var task = Task.Run(() => {
                var tempValue = channel.Output;
                var exitFor = func(tempValue);

                return exitFor;
            });

            this.tasks.Add(task);

            return this;
        }

        public GoSelect Case<T>(GoChannel<T> channel, ChannelDirection direction, T parameter, Action<T> act) {
            if (direction != ChannelDirection.Input) {
                throw new Exception(@"[ChannelDirection] must be ""Input""");
            }

            var task = Task.Run(() => {
                channel.Input = parameter;
                act(parameter);

                return new bool?(false);
            });

            this.tasks.Add(task);

            return this;
        }

        public GoSelect Case<T>(T parameter, GoChannel<T> channel, ChannelDirection direction, Action<T> act) {
            if (direction != ChannelDirection.Output) {
                throw new Exception(@"[ChannelDirection] must be ""Output""");
            }

            var task = Task.Run(() => {
                parameter = channel.Output;
                act(parameter);

                return new bool?(false);
            });

            this.tasks.Add(task);

            return this;
        }

        public GoSelect Case<T>(GoChannel<T> channel, ChannelDirection direction, Action<T> act) {
            if (direction != ChannelDirection.Output) {
                throw new Exception(@"[ChannelDirection] must be ""Output""");
            }

            var task = Task.Run(() => {
                var tempValue = channel.Output;
                act(tempValue);

                return new bool?(false);
            });

            this.tasks.Add(task);

            return this;
        }

        public GoSelect Case(DateTime timeout, Func<DateTime, bool?> func) {
            var task = Task.Run(() => {
                var exitFor = func(timeout);

                return exitFor;
            });

            this.tasks.Add(task);

            return this;
        }

        public GoSelect Case(DateTime timeout, Action<DateTime> act) {
            var task = Task.Run(() => {
                act(timeout);

                return new bool?(false);
            });

            this.tasks.Add(task);

            return this;
        }

        public GoSelect Default(Func<bool?> func) {
            var task = Task.Run(() => {
                var exitFor = func();

                return exitFor;
            });

            this.tasks.Add(task);

            return this;
        }

        public GoSelect Default(Action act) {
            var task = Task.Run(() => {
                act();

                return new bool?(false);
            });

            this.tasks.Add(task);

            return this;
        }

        public bool End() {
            var firstCompletedTask = Task.WhenAny(this.tasks).GetAwaiter().GetResult();

            return firstCompletedTask.GetAwaiter().GetResult().GetValueOrDefault(false);
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
