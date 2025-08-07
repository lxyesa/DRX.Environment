// MathUtils 静态类测试
console.log("Add(3,5):", MathUtils.Add(3, 5));
console.log("Subtract(10,4):", MathUtils.Subtract(10, 4));
console.log("Multiply(6,7):", MathUtils.Multiply(6, 7));
console.log("Divide(8,2):", MathUtils.Divide(8, 2));

// Person 普通类测试
var p = new Person("Alice", 30);
console.log("Person.Name:", p.Name);
console.log("Person.Age:", p.Age);
console.log("Person.Greet():", p.Greet());

// StringHelper 静态类测试
console.log("ToUpper('abc'):", StringHelper.ToUpper("abc"));
console.log("ToLower('ABC'):", StringHelper.ToLower("ABC"));
console.log("IsNullOrEmpty(''):", StringHelper.IsNullOrEmpty(""));
console.log("Reverse('hello'):", StringHelper.Reverse("hello"));

// 错误处理与重试机制测试
try {
    MathUtils.Divide(1, 0);
} catch (e) {
    console.log("捕获异常:", e.message || e);
}

// 异步执行测试（假设支持Promise/async）
(async function() {
    let result = await MathUtils.Add(10, 20);
    console.log("Async Add(10,20):", result);
})();