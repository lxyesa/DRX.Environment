// ESM 模块：导入其他 ESM 模块
import greet, { VERSION, add } from './simple-export.mjs';

export const message = greet("World");
export const ver = VERSION;
export const sum = add(2, 3);
