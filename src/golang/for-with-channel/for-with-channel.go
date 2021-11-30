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
