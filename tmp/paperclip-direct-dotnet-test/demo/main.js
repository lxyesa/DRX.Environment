const { log, fs, dotnet } = Paperclip.use();

const Path = dotnet.io.Path();
const DateTime = dotnet.runtime.DateTime();

const outputPath = Path.Combine('.', 'direct-dotnet.txt');
const line = DateTime.Now.ToString('yyyy-MM-dd HH:mm:ss');

fs.writeText(outputPath, line);
log.info('WROTE=' + outputPath);
