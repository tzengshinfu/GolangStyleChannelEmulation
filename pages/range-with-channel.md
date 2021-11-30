這是Golang的Channel + range關鍵字，

作用為持續取出Channel的值，直到Channel被關閉。

[source code](/src/golang/range-with-channel)
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

		fmt.Println("[數值寫入10次]啟動(阻塞開始)")
		for count := 1; count <= 10; count++ {
			channel <- count
			fmt.Println("[數值]" + strconv.Itoa(count) + "寫入[channel]")

			time.Sleep(time.Second)
		}
		fmt.Println("[數值寫入10次]完成(阻塞結束)")

		close(channel)
		fmt.Println("[channel]關閉")

		fmt.Println("[Goroutine執行緒]完成")
	}()

	fmt.Println("以range關鍵字取出[channel]結果啟動")
	for item := range channel {
		fmt.Println("取出<-" + strconv.Itoa(item))
	}
	fmt.Println("以range關鍵字取出[channel]結果完成")

	fmt.Println("[主執行緒]完成")
}
```
C#版本則可用foreach關鍵字 + Channel.Range屬性達到相同目的。

[source code](/src/csharp/RangeWithChannel)
```csharp
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
從Range屬性取出[channel]結果啟動
[Goroutine執行緒]啟動
[數值寫入10次]啟動(阻塞開始)
[數值]1寫入[channel]
取出<-1
[數值]2寫入[channel]
取出<-2
[數值]3寫入[channel]
取出<-3
[數值]4寫入[channel]
取出<-4
[數值]5寫入[channel]
取出<-5
[數值]6寫入[channel]
取出<-6
[數值]7寫入[channel]
取出<-7
[數值]8寫入[channel]
取出<-8
[數值]9寫入[channel]
取出<-9
[數值]10寫入[channel]
取出<-10
[數值寫入10次]完成(阻塞結束)
[channel]關閉
[Goroutine執行緒]完成
從Range屬性取出[channel]結果完成
[主執行緒]完成
```
[回上一層](../../../)
