const { log, fs, dotnet } = Paperclip.use(['log', 'fs', 'dotnet']);
const Path = dotnet.io.Path();
const output = Path.Combine('.', 'models-ok.txt');
fs.writeText(output, 'ok');
log.info('MODELS_OK=' + output);
