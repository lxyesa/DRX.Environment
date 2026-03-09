// ESM 导入 CJS 模块
import cjsMod from './simple-cjs.cjs';

export const magicValue = cjsMod.MAGIC;
export const product = cjsMod.multiply(3, 4);
