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
