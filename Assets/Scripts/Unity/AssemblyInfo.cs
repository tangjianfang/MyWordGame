using System.Runtime.CompilerServices;

// m5 C2：ChunkStreamer 的毫秒时钟 StopwatchMillis 以 internal 暴露，测试程序集
// （MyWorld.Core.Tests）通过 InternalsVisibleTo 注入假计时器，构造
// 「单件工作 5ms」这类确定性预算场景；生产代码无法从程序集外改写。
[assembly: InternalsVisibleTo("MyWorld.Core.Tests")]
