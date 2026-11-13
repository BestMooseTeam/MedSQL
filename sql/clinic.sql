-- ============================================================================
-- Задание 2. База клиники на SQL (руками, без C#)
-- База: clinic. Выполнять в pgAdmin → Query Tool
-- ============================================================================

-- ---------------------------------------------------------------------------
-- Шаг 2. Таблицы
-- ---------------------------------------------------------------------------

-- Картотека пациентов. phone может быть пустым: телефон есть не у всех
CREATE TABLE patients (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    birth_date DATE NOT NULL,
    phone VARCHAR(20)
);

-- Каталог видов справок. CHECK не даст сохранить нулевую или отрицательную цену
CREATE TABLE spravka_types (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    price NUMERIC(10, 2) NOT NULL CHECK (price > 0),
    valid_days INT NOT NULL
);

-- Выписанные справки. Две ссылки на другие таблицы, поэтому создаётся последней.
-- price здесь свой: это цена, за которую справку выдали, она не должна меняться
-- вместе с каталогом
CREATE TABLE spravki (
    id SERIAL PRIMARY KEY,
    patient_id INT NOT NULL REFERENCES patients(id),
    spravka_type_id INT NOT NULL REFERENCES spravka_types(id),
    price NUMERIC(10, 2) NOT NULL,
    issued_at DATE NOT NULL DEFAULT CURRENT_DATE
);

-- ---------------------------------------------------------------------------
-- Шаг 3. Данные
-- ---------------------------------------------------------------------------

-- Пять пациентов из задания
INSERT INTO patients (name, birth_date, phone) VALUES
    ('Айбек Асанов', '1998-03-12', '0555123456'),
    ('Нургуль Токтосунова', '2003-07-25', '0700987654'),
    ('Бакыт Жумабаев', '1985-11-02', NULL),
    ('Айпери Садыкова', '2001-01-30', '0772345678'),
    ('Тимур Ким', '2006-09-14', '0509111222');

-- Свои данные: ещё 3 пациента (минимум по заданию)
INSERT INTO patients (name, birth_date, phone) VALUES
    ('Айгуль Осмонова', '1995-06-18', '0555777888'),
    ('Данияр Абдылдаев', '2000-01-05', '0700111222'),
    ('Мария Петрова', '2010-12-31', '0500333444');

-- Четыре вида справок из задания
INSERT INTO spravka_types (name, price, valid_days) VALUES
    ('Справка 083', 100, 365),
    ('Санитарная книжка', 750, 365),
    ('Справка 086', 300, 180),
    ('Справка в бассейн', 200, 90);

-- Свой вид справки (минимум один по заданию)
INSERT INTO spravka_types (name, price, valid_days) VALUES
    ('Справка в детский лагерь', 500, 30);

-- Семь справок из задания
INSERT INTO spravki (patient_id, spravka_type_id, price, issued_at) VALUES
    (1, 1, 100, '2026-10-01'),
    (1, 4, 200, '2026-10-15'),
    (2, 2, 750, '2026-10-03'),
    (2, 3, 300, '2026-11-02'),
    (4, 3, 300, '2026-11-05'),
    (5, 4, 200, '2026-11-06'),
    (5, 1, 100, '2026-11-10');

-- Свои справки (минимум 5 по заданию)
INSERT INTO spravki (patient_id, spravka_type_id, price, issued_at) VALUES
    (3, 2, 750, '2026-10-20'),
    (6, 5, 500, '2026-11-01'),
    (7, 1, 100, '2026-11-03'),
    (8, 3, 300, '2026-11-07'),
    (6, 4, 200, '2026-11-08'),
    (7, 5, 500, '2026-11-12');

-- ---------------------------------------------------------------------------
-- Шаг 4. JOIN трёх таблиц
-- ---------------------------------------------------------------------------

-- Все справки с именем пациента и названием вида: в spravki лежат только номера,
-- поэтому соединяем три таблицы
SELECT spravki.id, patients.name AS patient, spravka_types.name AS spravka,
       spravki.price, spravki.issued_at
FROM spravki
JOIN patients ON spravki.patient_id = patients.id
JOIN spravka_types ON spravki.spravka_type_id = spravka_types.id
ORDER BY spravki.issued_at;

-- Статистика для главврача: сколько справок выписано и на какую сумму
SELECT COUNT(*) AS spravki_count, SUM(price) AS total FROM spravki;

-- ---------------------------------------------------------------------------
-- Шаг 5. Запросы для клиники
-- ---------------------------------------------------------------------------

-- 1. Регистратура: все пациенты по алфавиту
SELECT id, name, birth_date, phone
FROM patients
ORDER BY name;

-- 2. Пациенты, родившиеся в 2000 году и позже
SELECT id, name, birth_date
FROM patients
WHERE birth_date >= '2000-01-01'
ORDER BY birth_date;

-- 3. Пациенты без телефона.
--    WHERE phone = NULL не находит НИЧЕГО: NULL это «неизвестно», и сравнение
--    с ним даёт не true, а NULL (тоже «неизвестно»), то есть строка не проходит
--    условие. Для проверки на пустоту есть специальный оператор IS NULL
SELECT id, name
FROM patients
WHERE phone IS NULL;

-- 3а. Для сравнения: так возвращается пустой результат, хотя Бакыт без телефона
SELECT id, name
FROM patients
WHERE phone = NULL;

-- 4. Поиск пациента по части имени.
--    LIKE учитывает регистр: 'ай' не найдёт «Айбек»
SELECT id, name
FROM patients
WHERE name LIKE '%ай%';

-- 4а. ILIKE регистр не учитывает — находит и «Айбек», и «айбек»
SELECT id, name
FROM patients
WHERE name ILIKE '%ай%';

-- 5. Карточка пациента: все справки пациента с id 1, с названием, ценой и датой
SELECT spravki.id, spravka_types.name AS spravka, spravki.price, spravki.issued_at
FROM spravki
JOIN spravka_types ON spravki.spravka_type_id = spravka_types.id
WHERE spravki.patient_id = 1
ORDER BY spravki.issued_at;

-- 6. Справки, выписанные в ноябре 2026
SELECT id, patient_id, spravka_type_id, price, issued_at
FROM spravki
WHERE issued_at >= '2026-11-01' AND issued_at < '2026-12-01'
ORDER BY issued_at;

-- 7. Самая дорогая справка в каталоге
SELECT id, name, price
FROM spravka_types
ORDER BY price DESC
LIMIT 1;

-- 8. Главврач: сколько справок выписано в ноябре и на какую сумму
SELECT COUNT(*) AS spravki_count, SUM(price) AS total
FROM spravki
WHERE issued_at >= '2026-11-01' AND issued_at < '2026-12-01';

-- 9. Каталог подорожал: поднимаем цену одного вида справки
UPDATE spravka_types
SET price = 900
WHERE name = 'Санитарная книжка';

-- 9а. Уже выданные справки не подорожали: в spravki своя цена на момент выдачи
SELECT spravka_types.name, spravka_types.price AS catalog_price, spravki.price AS sold_price
FROM spravki
JOIN spravka_types ON spravki.spravka_type_id = spravka_types.id
WHERE spravka_types.name = 'Санитарная книжка';

-- 10. Удаляем одну справку
DELETE FROM spravki
WHERE id = 8;

-- 11. Бонус: сколько справок у каждого пациента.
--     LEFT JOIN нужен, чтобы в списке остались и пациенты без справок (COUNT даст 0)
SELECT patients.id, patients.name, COUNT(spravki.id) AS spravki_count
FROM patients
LEFT JOIN spravki ON spravki.patient_id = patients.id
GROUP BY patients.id, patients.name
ORDER BY spravki_count DESC, patients.name;

-- ---------------------------------------------------------------------------
-- Шаг 6. База защищает данные
-- ---------------------------------------------------------------------------

-- Справка несуществующему пациенту: нарушен внешний ключ
-- ERROR: insert or update on table "spravki" violates foreign key constraint "spravki_patient_id_fkey"
INSERT INTO spravki (patient_id, spravka_type_id, price) VALUES (999, 1, 100);

-- Отрицательная цена в каталоге: нарушен CHECK
-- ERROR: new row for relation "spravka_types" violates check constraint "spravka_types_price_check"
INSERT INTO spravka_types (name, price, valid_days) VALUES ('Справка в лагерь', -500, 30);

-- Пациент без имени: нарушен NOT NULL
-- ERROR: null value in column "name" of relation "patients" violates not-null constraint
INSERT INTO patients (birth_date) VALUES ('2000-01-01');

-- Удаление пациента, на которого ссылаются справки: нарушен внешний ключ
-- ERROR: update or delete on table "patients" violates foreign key constraint "spravki_patient_id_fkey"
DELETE FROM patients WHERE id = 1;

-- Номера с пропусками: неудачная вставка с patient_id 999 забрала номер 8,
-- поэтому правильная вставка получит 9. Номера идут по порядку, могут иметь
-- пропуски, но никогда не повторяются
INSERT INTO spravki (patient_id, spravka_type_id, price) VALUES (3, 2, 750);
SELECT * FROM spravki WHERE patient_id = 3;
