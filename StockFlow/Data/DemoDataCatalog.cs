using StockFlow.Entities;

namespace StockFlow.Data;

internal static class DemoDataCatalog
{
    internal const int ExpectedCategoryCount = 6;
    internal const int ExpectedProductCount = 30;
    internal const int ExpectedCustomerCount = 10;
    internal const int ExpectedSupplierCount = 6;
    internal const int ExpectedOrderCount = 24;
    internal const int ExpectedOrderItemCount = 54;
    internal const int ExpectedStockMovementCount = 30;
    internal const int ExpectedLowStockProductCount = 8;

    private static readonly string[] CategoryNames =
    [
        "Kurgusal Ofis Teknolojileri",
        "Kurgusal Depo Ekipmanları",
        "Kurgusal Paketleme Ürünleri",
        "Kurgusal İş Güvenliği",
        "Kurgusal Sarf Malzemeleri",
        "Kurgusal İşletme Mobilyaları"
    ];

    private static readonly DemoProductSeed[] ProductSeeds =
    [
        new(0, "Demo Dizüstü Bilgisayar", "DEMO-TEC-001", 24750.00m, 12, 4),
        new(0, "Demo 24 İnç Monitör", "DEMO-TEC-002", 6850.50m, 18, 5),
        new(0, "Demo Kablosuz Klavye", "DEMO-TEC-003", 975.90m, 30, 8),
        new(0, "Demo Barkod Okuyucu", "DEMO-TEC-004", 4320.00m, 8, 3),
        new(0, "Demo Etiket Yazıcı", "DEMO-TEC-005", 5890.00m, 6, 2),

        new(1, "Demo Modüler Depo Rafı", "DEMO-DEP-101", 8250.00m, 10, 3),
        new(1, "Demo El Terminali", "DEMO-DEP-102", 13250.00m, 7, 2),
        new(1, "Demo Taşıma Arabası", "DEMO-DEP-103", 3640.00m, 9, 3),
        new(1, "Demo Hassas Terazi", "DEMO-DEP-104", 2190.00m, 5, 2),
        new(1, "Demo Stok Kutusu", "DEMO-DEP-105", 285.75m, 40, 10),

        new(2, "Demo Koli 40x30", "DEMO-PAK-201", 32.50m, 80, 20),
        new(2, "Demo Paketleme Bandı", "DEMO-PAK-202", 48.90m, 100, 25),
        new(2, "Demo Streç Film", "DEMO-PAK-203", 189.75m, 60, 15),
        new(2, "Demo Kargo Zarfı", "DEMO-PAK-204", 12.40m, 120, 30),
        new(2, "Demo Termal Etiket", "DEMO-PAK-205", 145.00m, 90, 25),

        new(3, "Demo Koruyucu Baret", "DEMO-GUV-301", 420.00m, 25, 6),
        new(3, "Demo Reflektörlü Yelek", "DEMO-GUV-302", 310.50m, 30, 8),
        new(3, "Demo Koruyucu Eldiven", "DEMO-GUV-303", 95.90m, 50, 15),
        new(3, "Demo Koruyucu Gözlük", "DEMO-GUV-304", 185.00m, 20, 5),
        new(3, "Demo İlk Yardım Seti", "DEMO-GUV-305", 780.00m, 12, 4),

        new(4, "Demo A4 Kağıt Paketi", "DEMO-SAR-401", 235.50m, 70, 20),
        new(4, "Demo Toner Kartuşu", "DEMO-SAR-402", 1450.00m, 15, 5),
        new(4, "Demo Tükenmez Kalem", "DEMO-SAR-403", 18.75m, 100, 25),
        new(4, "Demo Not Defteri", "DEMO-SAR-404", 64.90m, 60, 15),
        new(4, "Demo Pil Paketi", "DEMO-SAR-405", 129.50m, 35, 10),

        new(5, "Demo Çalışma Masası", "DEMO-MOB-501", 6250.00m, 8, 3),
        new(5, "Demo Ofis Koltuğu", "DEMO-MOB-502", 4850.00m, 10, 3),
        new(5, "Demo Arşiv Dolabı", "DEMO-MOB-503", 7150.00m, 6, 2),
        new(5, "Demo Toplantı Konsolu", "DEMO-MOB-504", 12400.00m, 4, 2),
        new(5, "Demo Ziyaretçi Sandalyesi", "DEMO-MOB-505", 2150.00m, 16, 5)
    ];

    private static readonly DemoOrderSeed[] OrderSeeds =
    [
        Order("DEMO-PO-20260602-001", OrderType.Purchase, OrderStatus.Confirmed, Utc(6, 2), 0, true,
            Item("DEMO-TEC-001", 5), Item("DEMO-DEP-101", 4), Item("DEMO-DEP-105", 10)),
        Order("DEMO-SO-20260605-002", OrderType.Sale, OrderStatus.Confirmed, Utc(6, 5), 0, false,
            Item("DEMO-TEC-004", 5), Item("DEMO-TEC-003", 5), Item("DEMO-PAK-202", 20)),
        Order("DEMO-PO-20260609-003", OrderType.Purchase, OrderStatus.Confirmed, Utc(6, 9), 1, false,
            Item("DEMO-TEC-002", 6), Item("DEMO-DEP-103", 5), Item("DEMO-PAK-201", 20)),
        Order("DEMO-SO-20260613-004", OrderType.Sale, OrderStatus.Confirmed, Utc(6, 13), 1, true,
            Item("DEMO-TEC-005", 4), Item("DEMO-PAK-201", 15)),
        Order("DEMO-PO-20260618-005", OrderType.Purchase, OrderStatus.Confirmed, Utc(6, 18), 2, false,
            Item("DEMO-TEC-003", 10), Item("DEMO-PAK-202", 25)),
        Order("DEMO-SO-20260622-006", OrderType.Sale, OrderStatus.Confirmed, Utc(6, 22), 2, false,
            Item("DEMO-DEP-102", 5), Item("DEMO-TEC-002", 4), Item("DEMO-SAR-401", 10)),
        Order("DEMO-PO-20260628-007", OrderType.Purchase, OrderStatus.Confirmed, Utc(6, 28), 3, true,
            Item("DEMO-PAK-203", 15), Item("DEMO-GUV-301", 10), Item("DEMO-GUV-303", 20)),
        Order("DEMO-SO-20260704-008", OrderType.Sale, OrderStatus.Confirmed, Utc(7, 4), 3, false,
            Item("DEMO-DEP-104", 3), Item("DEMO-PAK-203", 10)),
        Order("DEMO-PO-20260710-009", OrderType.Purchase, OrderStatus.Confirmed, Utc(7, 10), 4, false,
            Item("DEMO-SAR-401", 20), Item("DEMO-MOB-502", 5)),
        Order("DEMO-SO-20260716-010", OrderType.Sale, OrderStatus.Confirmed, Utc(7, 16), 4, true,
            Item("DEMO-GUV-305", 8), Item("DEMO-GUV-302", 5), Item("DEMO-SAR-403", 15)),
        Order("DEMO-SO-20260724-011", OrderType.Sale, OrderStatus.Confirmed, Utc(7, 24), 5, false,
            Item("DEMO-SAR-402", 10), Item("DEMO-PAK-204", 20)),
        Order("DEMO-SO-20260801-012", OrderType.Sale, OrderStatus.Confirmed, Utc(8, 1), 6, false,
            Item("DEMO-MOB-503", 4), Item("DEMO-MOB-504", 2)),

        Order("DEMO-SO-20260803-013", OrderType.Sale, OrderStatus.Cancelled, Utc(8, 3), 7, true,
            Item("DEMO-TEC-001", 1), Item("DEMO-PAK-205", 5)),
        Order("DEMO-PO-20260806-014", OrderType.Purchase, OrderStatus.Draft, Utc(8, 6), 5, false,
            Item("DEMO-TEC-004", 2), Item("DEMO-TEC-005", 1)),
        Order("DEMO-SO-20260809-015", OrderType.Sale, OrderStatus.Draft, Utc(8, 9), 8, false,
            Item("DEMO-DEP-101", 1), Item("DEMO-DEP-105", 4)),
        Order("DEMO-PO-20260811-016", OrderType.Purchase, OrderStatus.Cancelled, Utc(8, 11), 0, true,
            Item("DEMO-DEP-102", 3), Item("DEMO-DEP-104", 2)),
        Order("DEMO-SO-20260814-017", OrderType.Sale, OrderStatus.Draft, Utc(8, 14), 9, false,
            Item("DEMO-TEC-002", 2), Item("DEMO-TEC-003", 3), Item("DEMO-PAK-201", 6)),
        Order("DEMO-SO-20260817-018", OrderType.Sale, OrderStatus.Cancelled, Utc(8, 17), 0, false,
            Item("DEMO-DEP-103", 2), Item("DEMO-GUV-304", 4)),
        Order("DEMO-PO-20260820-019", OrderType.Purchase, OrderStatus.Draft, Utc(8, 20), 1, true,
            Item("DEMO-GUV-305", 5), Item("DEMO-SAR-402", 4)),
        Order("DEMO-SO-20260822-020", OrderType.Sale, OrderStatus.Draft, Utc(8, 22), 1, false,
            Item("DEMO-GUV-301", 2)),
        Order("DEMO-PO-20260824-021", OrderType.Purchase, OrderStatus.Cancelled, Utc(8, 24), 2, false,
            Item("DEMO-SAR-404", 10), Item("DEMO-SAR-405", 6)),
        Order("DEMO-PO-20260826-022", OrderType.Purchase, OrderStatus.Draft, Utc(8, 26), 3, true,
            Item("DEMO-MOB-501", 2), Item("DEMO-MOB-505", 4)),
        Order("DEMO-SO-20260828-023", OrderType.Sale, OrderStatus.Cancelled, Utc(8, 28), 2, false,
            Item("DEMO-PAK-202", 8), Item("DEMO-PAK-204", 12)),
        Order("DEMO-SO-20260830-024", OrderType.Sale, OrderStatus.Draft, Utc(8, 30), 3, false,
            Item("DEMO-GUV-303", 5), Item("DEMO-SAR-403", 10))
    ];

    internal static DemoDataSet Create(string adminUserId, string employeeUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adminUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeUserId);

        var categories = CategoryNames
            .Select(name => new Category { Name = name })
            .ToList();
        var products = ProductSeeds
            .Select(seed => new Product
            {
                Name = seed.Name,
                Sku = seed.Sku,
                Price = seed.Price,
                StockQuantity = seed.OpeningStockQuantity,
                MinimumStockQuantity = seed.MinimumStockQuantity,
                Category = categories[seed.CategoryIndex]
            })
            .ToList();
        var productsBySku = products.ToDictionary(product => product.Sku, StringComparer.Ordinal);

        var customers = Enumerable.Range(1, ExpectedCustomerCount)
            .Select(index => new Customer
            {
                Name = $"Kurgusal Demo Müşteri {index:00}",
                Email = $"customer{index:00}@example.com",
                Phone = $"+90 000 000 00 {index:00}",
                Address = $"Kurgusal Demo Mahallesi, Örnek Sokak No: {index}"
            })
            .ToList();
        var suppliers = Enumerable.Range(1, ExpectedSupplierCount)
            .Select(index => new Supplier
            {
                CompanyName = $"Kurgusal Demo Tedarikçi {index:00}",
                Email = $"supplier{index:00}@example.org",
                Phone = $"+90 000 100 00 {index:00}",
                Address = $"Kurgusal Lojistik Bölgesi, Demo Cadde No: {index}"
            })
            .ToList();

        var orders = new List<Order>(OrderSeeds.Length);
        for (var index = 0; index < OrderSeeds.Length; index++)
        {
            var seed = OrderSeeds[index];
            var order = CreateOrder(
                seed,
                productsBySku,
                customers,
                suppliers,
                seed.CreatedByAdmin ? adminUserId : employeeUserId);
            orders.Add(order);
        }

        EnsureExpectedShape(categories, products, customers, suppliers, orders);
        return new DemoDataSet(categories, products, customers, suppliers, orders);
    }

    internal static IReadOnlyDictionary<string, int> GetOpeningStockQuantities()
    {
        return ProductSeeds.ToDictionary(
            seed => seed.Sku,
            seed => seed.OpeningStockQuantity,
            StringComparer.Ordinal);
    }

    private static Order CreateOrder(
        DemoOrderSeed seed,
        IReadOnlyDictionary<string, Product> productsBySku,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<Supplier> suppliers,
        string createdByUserId)
    {
        if (seed.Items.Count == 0
            || seed.Items.Any(item => item.Quantity <= 0)
            || seed.Items.Select(item => item.ProductSku).Distinct(StringComparer.Ordinal).Count() != seed.Items.Count)
        {
            throw new InvalidOperationException("Demo sipariş kalem tanımları geçersizdir.");
        }

        var order = new Order
        {
            OrderNumber = seed.OrderNumber,
            Type = seed.Type,
            Status = seed.Status,
            OrderDate = seed.OrderDate,
            CreatedByUserId = createdByUserId
        };

        if (seed.Type == OrderType.Sale)
        {
            order.Customer = customers[seed.PartyIndex];
        }
        else if (seed.Type == OrderType.Purchase)
        {
            order.Supplier = suppliers[seed.PartyIndex];
        }
        else
        {
            throw new InvalidOperationException("Demo sipariş türü geçersizdir.");
        }

        decimal totalAmount = 0;
        foreach (var itemSeed in seed.Items)
        {
            if (!productsBySku.TryGetValue(itemSeed.ProductSku, out var product))
            {
                throw new InvalidOperationException("Demo sipariş kalemi bilinmeyen bir SKU içeriyor.");
            }

            totalAmount = checked(totalAmount + (product.Price * itemSeed.Quantity));
            order.Items.Add(new OrderItem
            {
                Order = order,
                Product = product,
                Quantity = itemSeed.Quantity,
                UnitPrice = product.Price
            });
        }

        order.TotalAmount = totalAmount;
        if (seed.Status == OrderStatus.Confirmed)
        {
            ApplyConfirmedStockEffects(order);
        }
        else if (seed.Status is not (OrderStatus.Draft or OrderStatus.Cancelled))
        {
            throw new InvalidOperationException("Demo sipariş durumu geçersizdir.");
        }

        return order;
    }

    private static void ApplyConfirmedStockEffects(Order order)
    {
        var movementType = order.Type == OrderType.Purchase
            ? StockMovementType.StockIn
            : StockMovementType.StockOut;
        var movementDate = order.OrderDate.AddHours(2);

        foreach (var item in order.Items)
        {
            item.Product.StockQuantity = movementType == StockMovementType.StockIn
                ? checked(item.Product.StockQuantity + item.Quantity)
                : item.Product.StockQuantity - item.Quantity;

            if (item.Product.StockQuantity < 0)
            {
                throw new InvalidOperationException("Demo satış siparişi açılış stok miktarını aşıyor.");
            }

            order.StockMovements.Add(new StockMovement
            {
                Order = order,
                Product = item.Product,
                Type = movementType,
                Quantity = item.Quantity,
                Description = $"Sipariş {order.OrderNumber}: " +
                    (movementType == StockMovementType.StockIn
                        ? "satın alma stok girişi."
                        : "satış stok çıkışı."),
                MovementDate = movementDate
            });
        }
    }

    private static void EnsureExpectedShape(
        IReadOnlyCollection<Category> categories,
        IReadOnlyCollection<Product> products,
        IReadOnlyCollection<Customer> customers,
        IReadOnlyCollection<Supplier> suppliers,
        IReadOnlyCollection<Order> orders)
    {
        var itemCount = orders.Sum(order => order.Items.Count);
        var movementCount = orders.Sum(order => order.StockMovements.Count);
        var lowStockCount = products.Count(product =>
            product.StockQuantity <= product.MinimumStockQuantity);

        if (categories.Count != ExpectedCategoryCount
            || products.Count != ExpectedProductCount
            || customers.Count != ExpectedCustomerCount
            || suppliers.Count != ExpectedSupplierCount
            || orders.Count != ExpectedOrderCount
            || itemCount != ExpectedOrderItemCount
            || movementCount != ExpectedStockMovementCount
            || lowStockCount != ExpectedLowStockProductCount)
        {
            throw new InvalidOperationException("Demo veri kümesi beklenen deterministik şekle sahip değil.");
        }
    }

    private static DemoOrderSeed Order(
        string orderNumber,
        OrderType type,
        OrderStatus status,
        DateTime orderDate,
        int partyIndex,
        bool createdByAdmin,
        params DemoOrderItemSeed[] items)
    {
        return new DemoOrderSeed(
            orderNumber,
            type,
            status,
            orderDate,
            partyIndex,
            createdByAdmin,
            items);
    }

    private static DemoOrderItemSeed Item(string productSku, int quantity)
    {
        return new DemoOrderItemSeed(productSku, quantity);
    }

    private static DateTime Utc(int month, int day)
    {
        return new DateTime(2026, month, day, 9, 0, 0, DateTimeKind.Utc);
    }

    private sealed record DemoProductSeed(
        int CategoryIndex,
        string Name,
        string Sku,
        decimal Price,
        int OpeningStockQuantity,
        int MinimumStockQuantity);

    private sealed record DemoOrderSeed(
        string OrderNumber,
        OrderType Type,
        OrderStatus Status,
        DateTime OrderDate,
        int PartyIndex,
        bool CreatedByAdmin,
        IReadOnlyList<DemoOrderItemSeed> Items);

    private sealed record DemoOrderItemSeed(string ProductSku, int Quantity);
}

internal sealed record DemoDataSet(
    IReadOnlyList<Category> Categories,
    IReadOnlyList<Product> Products,
    IReadOnlyList<Customer> Customers,
    IReadOnlyList<Supplier> Suppliers,
    IReadOnlyList<Order> Orders);
