// 循环依赖 A → B → A
import { valueB } from './circular-b.mjs';

export const valueA = "A";
export const fromB = valueB;
