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
