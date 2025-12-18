// Type definitions for sevenzip-wasm
declare module 'sevenzip-wasm' {
  interface SevenZipFS {
    writeFile(path: string, data: string | Uint8Array): void;
    readFile(path: string): Uint8Array;
    readFile(path: string, options: { encoding: 'utf8' }): string;
    mkdir(path: string): void;
    rmdir(path: string): void;
    unlink(path: string): void;
    readdir(path: string): string[];
    stat(path: string): { mode: number };
    isDir(mode: number): boolean;
    chdir(path: string): void;
  }

  interface SevenZipModule {
    callMain(args: string[]): number;
    FS: SevenZipFS;
  }

  interface SevenZipOptions {
    print?: (line: string) => void;
    printErr?: (line: string) => void;
  }

  function SevenZipWasm(options?: SevenZipOptions): Promise<SevenZipModule>;

  export default SevenZipWasm;
}
