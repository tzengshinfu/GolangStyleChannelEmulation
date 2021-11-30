這是Golang的Channel + for關鍵字，

在無限迴圈中持續取出Channel的值，直到Channel被關閉。

[source code](/src/golang/for-with-channel)
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

	fmt.Println("以for迴圈取出[channel]結果啟動")
	for {
		v, ok := <-channel

		if ok {
			fmt.Println("取出<-" + strconv.Itoa(v))
		} else {
			fmt.Println("偵測到[channel]已關閉，跳出迴圈")
			break
		}
	}
	fmt.Println("以for迴圈取出[channel]結果結束")

	fmt.Println("[主執行緒]結束")
}
```
C#版本則是在無限迴圈中，使用try/catch關鍵字捕捉Channel被關閉的例外。

[source code](/src/csharp/ForWithChannel)
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ForWithChannel {
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

            Console.WriteLine("以for迴圈取出[channel]結果啟動");
            for (; ; ) {
                try {
                    Console.WriteLine($"取出<-{channel.Output}");
                }
                catch (ChannelClosedException) {
                    Console.WriteLine("偵測到[channel]已關閉，跳出迴圈");
                    break;
                }
            }
            Console.WriteLine("以for迴圈取出[channel]結果結束");

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
            WriteAsync(channel, value).AsTask().GetAwaiter().GetResult();
        }
    }
    public T Output {
        get {
            return ReadAsync(channel).AsTask().GetAwaiter().GetResult();
        }
    }
    public IEnumerable<T> Range {
        get {
            return ReadAllAsync(channel).ToListAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public GoChannel(int? capacity = null) {
        this.capacity = capacity;
        this.spinWait = new SpinWait();

        //Buffered Channel
        if (capacity != null) {
            channel = Channel.CreateBounded<T>((int)capacity);
        }
        //Unbuffered Channel
        else {
            channel = Channel.CreateUnbounded<T>();
        }
    }

    public async ValueTask<T> ReadAsync(Channel<T> channel) {
        spinWait.Reset();

        while (true) {
            if (!await channel.Reader.WaitToReadAsync().ConfigureAwait(false)) {
                throw new ChannelClosedException();
            }

            if (channel.Reader.TryRead(out var item)) {
                return item;
            }

            //自旋等待，降低CPU使用率
            spinWait.SpinOnce();
        }
    }

    public async ValueTask WriteAsync(Channel<T> channel, T value) {
        spinWait.Reset();

        while (true) {
            if (!await channel.Writer.WaitToWriteAsync().ConfigureAwait(false)) {
                throw new ChannelClosedException();
            }

            if (channel.Writer.TryWrite(value)) {
                //Buffered Channel
                if (capacity != null) {
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
                        spinWait.SpinOnce();
                    }
                }
            }

            //自旋等待，降低CPU使用率
            spinWait.SpinOnce();
        }
    }

    public async IAsyncEnumerable<T> ReadAllAsync(Channel<T> channel) {
        await foreach (var value in channel.Reader.ReadAllAsync().ConfigureAwait(false)) {
            yield return value;
        }
    }

    public void Close() {
        channel.Writer.Complete();
    }
}
```
執行結果如下：
```console
[主執行緒]啟動
[channel]建立為Unbuffered Channel
以for迴圈取出[channel]結果啟動
[Goroutine執行緒]啟動
[數值寫入10次]啟動(阻塞開始)
[數值]1寫入[channel]
取出<-1
[數值]2寫入[channel]
取出<-2
取出<-3
[數值]3寫入[channel]
[數值]4寫入[channel]
取出<-4
取出<-5
[數值]5寫入[channel]
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
偵測到[channel]已關閉，跳出迴圈
以for迴圈取出[channel]結果結束
[主執行緒]結束
```
[回上一層](../../../)
