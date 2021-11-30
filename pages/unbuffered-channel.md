這是Golang的Unbuffered Channel。

[source code](/src/golang/unbuffered-channel)
```go
package main

import (
	"fmt"
	"strconv"
	"time"
)

func main() {
	fmt.Println("[主執行緒]啟動")

	channel := make(chan int)
	fmt.Println("[channel]建立為Unbuffered Channel")

	go func() {
		fmt.Println("[Goroutine執行緒]啟動")

		fmt.Println("[channel]推入1，阻塞開始直到被取出")
		channel <- 1

		fmt.Println("[channel]推入2，阻塞開始直到被取出")
		channel <- 2

		fmt.Println("[Goroutine執行緒]完成")
	}()

	time.Sleep(2 * time.Second)

	fmt.Println("取出<-" + strconv.Itoa(<-channel) + "，阻塞結束")

	time.Sleep(2 * time.Second)

	fmt.Println("取出<-" + strconv.Itoa(<-channel) + "，阻塞結束")

	time.Sleep(2 * time.Second)

	fmt.Println("[主執行緒]完成")
}
```
由於C#的Channel並不具Unbuffered的特性，

所以要自行建立GoChannel類別並以代碼實作Unbuffered Channel的

"推入後尚未拉取則進行阻塞"的特性。

[source code](/src/csharp/UnbufferedChannel)
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace UnbufferedChannel {
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("[主執行緒]啟動");

            var channel = new GoChannel<int>();
            Console.WriteLine("[channel]建立為Unbuffered Channel");

            Task.Run(() => {
                Console.WriteLine("[Goroutine執行緒]啟動");

                Console.WriteLine("[channel]推入1，阻塞開始直到被取出");
                channel.Input = 1;

                Console.WriteLine("[channel]推入2，阻塞開始直到被取出");
                channel.Input = 2;

                Console.WriteLine("[Goroutine執行緒]完成");
            });

            SpinWait.SpinUntil(() => false, TimeSpan.FromSeconds(2));

            Console.WriteLine($"取出<-{channel.Output}，阻塞結束");

            SpinWait.SpinUntil(() => false, TimeSpan.FromSeconds(2));

            Console.WriteLine($"取出<-{channel.Output}，阻塞結束");

            SpinWait.SpinUntil(() => false, TimeSpan.FromSeconds(2));

            Console.WriteLine("[主執行緒]完成");
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
執行結果如下：
```console
[主執行緒]啟動
[channel]建立為Unbuffered Channel
[Goroutine執行緒]啟動
[channel]推入1，阻塞開始直到被取出
[channel]推入2，阻塞開始直到被取出
取出<-1，阻塞結束
取出<-2，阻塞結束
[Goroutine執行緒]完成
[主執行緒]完成
```
[回上一層](../../../)
