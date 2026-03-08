/**
 * 模块名: paperclip.module.d.ts
 * 职责: 为 Paperclip 单文件 SDK（含 log/fs/stream/dotnet）提供全量 TypeScript 类型支持。
 * 依赖: Models/paperclip.module.js 运行时实现。
 */

declare global {
  interface PaperclipModuleContext {
    global: typeof globalThis;
    paperclip: PaperclipNamespace;
    requireBridge(api: string, method: string): (...args: unknown[]) => unknown;
    registerModule<TModule>(name: string, moduleValue: TModule, aliases?: readonly string[]): TModule;
  }

  interface PaperclipLogModule {
    log(...args: unknown[]): unknown;
    print(...args: unknown[]): unknown;
    info(...args: unknown[]): unknown;
    success(...args: unknown[]): unknown;
    debug(...args: unknown[]): unknown;
    trace(...args: unknown[]): unknown;
    warn(...args: unknown[]): unknown;
    error(...args: unknown[]): unknown;
    fatal(...args: unknown[]): unknown;
    logAsync(...args: unknown[]): Promise<unknown>;
    printAsync(...args: unknown[]): Promise<unknown>;
    infoAsync(...args: unknown[]): Promise<unknown>;
    successAsync(...args: unknown[]): Promise<unknown>;
    debugAsync(...args: unknown[]): Promise<unknown>;
    traceAsync(...args: unknown[]): Promise<unknown>;
    warnAsync(...args: unknown[]): Promise<unknown>;
    errorAsync(...args: unknown[]): Promise<unknown>;
    fatalAsync(...args: unknown[]): Promise<unknown>;
  }

  interface PaperclipFsModule {
    exists(path: string): boolean;

    readFile(path: string): string;
    readFileAsync(path: string): Promise<string>;
    writeFile(path: string, content: string): void;
    writeFileAsync(path: string, content: string): Promise<void>;
    appendFile(path: string, content: string): void;
    appendFileAsync(path: string, content: string): Promise<void>;
    readBinary(path: string): unknown;
    readBinaryAsync(path: string): Promise<unknown>;
    writeBinary(path: string, bytes: unknown): void;
    writeBinaryAsync(path: string, bytes: unknown): Promise<void>;
    deleteFile(path: string): void;
    deleteFileAsync(path: string): Promise<void>;
    copy(sourcePath: string, destinationPath: string, overwrite?: boolean): void;
    copyAsync(sourcePath: string, destinationPath: string, overwrite?: boolean): Promise<void>;
    move(sourcePath: string, destinationPath: string): void;
    moveAsync(sourcePath: string, destinationPath: string): Promise<void>;

    // 兼容旧接口
    readText(path: string): string;
    readTextAsync(path: string): Promise<string>;
    writeText(path: string, content: string): void;
    writeTextAsync(path: string, content: string): Promise<void>;
    appendText(path: string, content: string): void;
    appendTextAsync(path: string, content: string): Promise<void>;
    readBytes(path: string): unknown;
    readBytesAsync(path: string): Promise<unknown>;
    writeBytes(path: string, bytes: unknown): void;
    writeBytesAsync(path: string, bytes: unknown): Promise<void>;
    remove(path: string): void;
    removeAsync(path: string): Promise<void>;
    copyTo(sourcePath: string, destinationPath: string, overwrite?: boolean): void;
    copyToAsync(sourcePath: string, destinationPath: string, overwrite?: boolean): Promise<void>;
    moveTo(sourcePath: string, destinationPath: string): void;
    moveToAsync(sourcePath: string, destinationPath: string): Promise<void>;
  }

  interface PaperclipStreamModule {
    createReadStream(path: string): unknown;
    createReadStreamAsync(path: string): Promise<unknown>;
    createWriteStream(path: string): unknown;
    createWriteStreamAsync(path: string): Promise<unknown>;
    createAppendStream(path: string): unknown;
    createAppendStreamAsync(path: string): Promise<unknown>;
    read(stream: unknown, count: number): unknown;
    readAsync(stream: unknown, count: number): Promise<unknown>;
    write(stream: unknown, bytes: unknown): void;
    writeAsync(stream: unknown, bytes: unknown): Promise<void>;
    readText(stream: unknown): string;
    readTextAsync(stream: unknown): Promise<string>;
    position(stream: unknown): number;
    seek(stream: unknown, position: number): void;
    length(stream: unknown): number;

    // 兼容旧接口
    openRead(path: string): unknown;
    openReadAsync(path: string): Promise<unknown>;
    openWrite(path: string): unknown;
    openWriteAsync(path: string): Promise<unknown>;
    openAppend(path: string): unknown;
    openAppendAsync(path: string): Promise<unknown>;
    readBytes(stream: unknown, count: number): unknown;
    readBytesAsync(stream: unknown, count: number): Promise<unknown>;
    writeBytes(stream: unknown, bytes: unknown): void;
    writeBytesAsync(stream: unknown, bytes: unknown): Promise<void>;
    readToEnd(stream: unknown): string;
    readToEndAsync(stream: unknown): Promise<string>;
    writeText(stream: unknown, content: string): void;
    writeTextAsync(stream: unknown, content: string): Promise<void>;
    getPosition(stream: unknown): number;
    setPosition(stream: unknown, position: number): void;
    getLength(stream: unknown): number;
    flush(stream: unknown): void;
    flushAsync(stream: unknown): Promise<void>;
    close(stream: unknown): void;
    closeAsync(stream: unknown): Promise<void>;
  }

  interface PaperclipDotNetModule {
    type(typeName: string): unknown;
    requireType(typeName: string): unknown;
    io: {
      File: () => unknown;
      file: () => unknown;
      BridgeFile: () => unknown;
      bridgeFile: () => unknown;
      Path: () => unknown;
      path: () => unknown;
      Directory: () => unknown;
      directory: () => unknown;
      FileInfo: () => unknown;
      fileInfo: () => unknown;
      DirectoryInfo: () => unknown;
      directoryInfo: () => unknown;
    };
    text: {
      Encoding: () => unknown;
      encoding: () => unknown;
      StringBuilder: () => unknown;
      stringBuilder: () => unknown;
      JsonSerializer: () => unknown;
      jsonSerializer: () => unknown;
      Regex: () => unknown;
      regex: () => unknown;
    };
    runtime: {
      Environment: () => unknown;
      environment: () => unknown;
      DateTime: () => unknown;
      dateTime: () => unknown;
      DateOnly: () => unknown;
      dateOnly: () => unknown;
      TimeOnly: () => unknown;
      timeOnly: () => unknown;
      Guid: () => unknown;
      guid: () => unknown;
      TimeSpan: () => unknown;
      timeSpan: () => unknown;
      Convert: () => unknown;
      convert: () => unknown;
      Math: () => unknown;
      math: () => unknown;
      Console: () => unknown;
      console: () => unknown;
      Uri: () => unknown;
      uri: () => unknown;
      Random: () => unknown;
      random: () => unknown;
    };
    linq: {
      Enumerable: () => unknown;
      enumerable: () => unknown;
    };
    diagnostics: {
      Process: () => unknown;
      process: () => unknown;
      Stopwatch: () => unknown;
      stopwatch: () => unknown;
    };
    globalization: {
      CultureInfo: () => unknown;
      cultureInfo: () => unknown;
    };
    threading: {
      Task: () => unknown;
      task: () => unknown;
      CancellationTokenSource: () => unknown;
      cancellationTokenSource: () => unknown;
    };
    net: {
      HttpClient: () => unknown;
      httpClient: () => unknown;
      HttpRequestMessage: () => unknown;
      httpRequestMessage: () => unknown;
      HttpMethod: () => unknown;
      httpMethod: () => unknown;
      HttpStatusCode: () => unknown;
      httpStatusCode: () => unknown;
    };
  }

  interface PaperclipKnownModules {
    log: PaperclipLogModule;
    fs: PaperclipFsModule;
    stream: PaperclipStreamModule;
    dotnet: PaperclipDotNetModule;
    [key: string]: unknown;
  }

  interface PaperclipNamespace {
    registerModule<TModule>(name: string, moduleValue: TModule, aliases?: readonly string[]): TModule;
    requireMethod(api: string, method: string): (...args: unknown[]) => unknown;
    requireBridge(api: string, method: string): (...args: unknown[]) => unknown;
    createModule<TModule>(
      name: string,
      factory: (ctx: PaperclipModuleContext) => TModule,
      aliases?: readonly string[]
    ): TModule;
    getModule<TModule = unknown>(name: string): TModule | undefined;
    use(): PaperclipKnownModules;
    use<K extends keyof PaperclipKnownModules>(names: readonly K[]): Pick<PaperclipKnownModules, K>;
  }

  const Paperclip: PaperclipNamespace;
}

export {};
