這是Golang的Channel + select關鍵字，

作用為同時設定多個Channel推入/拉出/阻塞時的處理方式。

[source code](/src/golang/select-with-channel)
```go
package main

import (
	"fmt"
	"strconv"
)

func main() {
	fmt.Println("[主執行緒]啟動")

	mainChannel := make(chan int)
	fmt.Println("[mainChannel]建立為Unbuffered Channel")

	quitChannel := make(chan int)
	fmt.Println("[quitChannel]建立為Unbuffered Channel")

	go func() {
		fmt.Println("[Goroutine執行緒]啟動")

		fmt.Println("[數值]從[mainChannel]取出啟動(阻塞開始)")
		for count := 1; count <= 10; count++ {
			fmt.Println("取出<-" + strconv.Itoa(<-mainChannel))
		}
		fmt.Println("[數值]從[mainChannel]取出完成(阻塞結束)")

		quitChannel <- 0
		fmt.Println("[數值]0寫入[quitChannel]")

		fmt.Println("[Goroutine執行緒]完成")
	}()

	x := 0
	fmt.Println("x = 0")

	fmt.Println("以for迴圈重覆執行select啟動")
	for {
		fmt.Println("進入select")
		select {
		case mainChannel <- x:
			fmt.Println("[數值]" + strconv.Itoa(x) + "寫入[mainChannel]")
			x += 2
			fmt.Println("x += 2")
		case <-quitChannel:
			fmt.Println("離開for迴圈")

			return
		}
		fmt.Println("離開select")
	}
	fmt.Println("以for迴圈重覆執行select結束")

	fmt.Println("[主執行緒]結束")
}
```
以C#模擬如下：

*`(未完成)`</span>在條件達到離開迴圈時會因為設計問題造成時間差，程式將進入無限期阻塞而無法完成，此bug尚在尋找解決方案。*

[source code](/src/csharp/SelectWithChannel)
```csharp
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
}
```
需定義GoChannel類別如下：
```csharp
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
```
以及定義GoSelect類別如下：
```csharp
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
```
執行結果如下：
```console
[主執行緒]啟動
[mainChannel]建立為Unbuffered Channel
[quitChannel]建立為Unbuffered Channel
x = 0
以for迴圈重覆執行select啟動
進入select
[Goroutine執行緒]啟動
[數值]從[mainChannel]取出啟動(阻塞開始)
[數值]0寫入[mainChannel]
x += 2
離開select
進入select
取出<-0
取出<-2
[數值]2寫入[mainChannel]
x += 2
離開select
進入select
[數值]4寫入[mainChannel]
x += 2
離開select
進入select
取出<-4
取出<-6
[數值]6寫入[mainChannel]
x += 2
離開select
進入select
[數值]8寫入[mainChannel]
x += 2
離開select
進入select
取出<-8
取出<-10
[數值]10寫入[mainChannel]
x += 2
離開select
進入select
[數值]12寫入[mainChannel]
x += 2
離開select
進入select
取出<-12
取出<-14
[數值]14寫入[mainChannel]
x += 2
離開select
進入select
[數值]16寫入[mainChannel]
x += 2
離開select
進入select
取出<-16
取出<-18
[數值]18寫入[mainChannel]
x += 2
離開select
進入select
[數值]從[mainChannel]取出完成(阻塞結束)
[數值]0寫入[quitChannel]
[Goroutine執行緒]完成--->C#版本隨即進入無限期阻塞而無法離開for迴圈
離開for迴圈
```
[回上一層](../../../)
