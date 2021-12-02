# 用C#實作Golang風格的Channel
C#也有與Golang作用相似的Channel([System.Threading.Channels](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/))，

不過個人覺得在使用方面因為async/await關鍵字加上非同步方法命名，

看起來比較冗長繁瑣；但因為C#本身可在同步/非同步間切換的特性，

所以讓"建立Golang風格的Channel物件"的這件事變成可能。

&nbsp;

本倉庫的代碼只是概念驗證(或可說是許願清單)，著重在Channel使用的簡化，

~~並未對效能方面進行任何驗證~~請勿用於生產環境！

(P.S.:以BenchmarkDotNet測量，吞吐效能下降至原本約1/3。)

&nbsp;

[實作Channel(Unbuffered)](/pages/unbuffered-channel.md)

[實作Channel + for關鍵字](/pages/for-with-channel.md)

[實作Channel + range關鍵字](/pages/range-with-channel.md)

[實作Channel + select關鍵字(未完成)](/pages/select-with-channel.md)
