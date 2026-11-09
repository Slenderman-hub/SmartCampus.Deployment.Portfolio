# ПК21. SAT Protocol Report

- АРМ: `ARM-KPP-01`
- Окружение: `Staging`
- API: `https://httpbin.org/headers`

| ID | Проверка | Результат | Детали |
|---|---|---|---|
| SAT-01 | Корректность идентификатора АРМ | PASSED | 'ARM-KPP-01' соответствует шаблону ARM-XXX-NN |
| SAT-02 | Доступность внешнего API | PASSED | HTTP 200, 721 мс (порог 3000 мс) |
| SAT-03 | Передача служебных заголовков | PASSED | X-Station-Id=ARM-KPP-01, X-Campus-Security передан |
| SAT-04 | Окружение развёртывания | PASSED | Staging входит в список Staging/Production |

**Итог: PASSED (4/4)**
