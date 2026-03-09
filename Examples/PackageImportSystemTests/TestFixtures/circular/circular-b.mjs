// 循环依赖 B → A → B
import { valueA } from './circular-a.mjs';

export const valueB = "B";
export const fromA = valueA;
