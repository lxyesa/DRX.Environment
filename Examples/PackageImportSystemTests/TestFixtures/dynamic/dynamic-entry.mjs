// 动态导入测试入口
export async function loadModule(path) {
    const mod = await import(path);
    return mod;
}
