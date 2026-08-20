using YtMusicTui.App;
using YtMusicTui.Config;
using YtMusicTui.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var config = AppConfig.Load();
var music = new MockMusicService();
var player = new MockPlayerService();

using var app = new MusicApp(config, music, player);
await app.RunAsync();
