using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Domain.Enums;

namespace PharmaFlow.Infrastructure.Data;

public static class DatabaseSeeder
{
    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }

    public static async Task SeedAsync(AppDbContext db)
    {
        // Admin user
        if (!await db.Users.AnyAsync())
        {
            var adminRole = new Role { Name = "管理员", Description = "系统管理员",
                Permissions = System.Text.Json.JsonSerializer.Serialize(new[] { "*" }) };
            db.Roles.Add(adminRole);
            db.Users.Add(new User { Username = "admin", PasswordHash = HashPassword("admin123"), DisplayName = "管理员", IsActive = true, Role = adminRole });
        }

        // Drug categories + drugs
        if (!await db.DrugCategories.AnyAsync())
        {
            var cats = new[] { "抗生素类", "心脑血管类", "消化系统类", "呼吸系统类", "中成药" };
            var catEntities = cats.Select(c => new DrugCategory { Name = c }).ToList();
            db.DrugCategories.AddRange(catEntities);
            await db.SaveChangesAsync();

            if (!await db.Drugs.AnyAsync())
            {
                var drugs = new List<Drug>
                {
                    new() { Code = "YP00001", Name = "阿莫西林胶囊", Specification = "0.5g*24粒", Manufacturer = "华北制药", Unit = "盒", CategoryId = catEntities[0].Id },
                    new() { Code = "YP00002", Name = "头孢克肟分散片", Specification = "50mg*12片", Manufacturer = "广州白云山", Unit = "盒", CategoryId = catEntities[0].Id },
                    new() { Code = "YP00003", Name = "阿托伐他汀钙片", Specification = "10mg*28片", Manufacturer = "辉瑞制药", Unit = "盒", CategoryId = catEntities[1].Id },
                    new() { Code = "YP00004", Name = "硝苯地平控释片", Specification = "30mg*12片", Manufacturer = "拜耳医药", Unit = "盒", CategoryId = catEntities[1].Id },
                    new() { Code = "YP00005", Name = "奥美拉唑肠溶胶囊", Specification = "20mg*14粒", Manufacturer = "江苏豪森", Unit = "盒", CategoryId = catEntities[2].Id },
                    new() { Code = "YP00006", Name = "孟鲁司特钠片", Specification = "10mg*5片", Manufacturer = "杭州默沙东", Unit = "盒", CategoryId = catEntities[3].Id },
                    new() { Code = "YP00007", Name = "连花清瘟胶囊", Specification = "0.35g*36粒", Manufacturer = "以岭药业", Unit = "盒", CategoryId = catEntities[4].Id },
                    new() { Code = "YP00008", Name = "板蓝根颗粒", Specification = "10g*20袋", Manufacturer = "同仁堂科技", Unit = "袋", CategoryId = catEntities[4].Id },
                };
                db.Drugs.AddRange(drugs);
                await db.SaveChangesAsync();
            }
        }

        // Customers with provinces for channel conflict detection
        if (!await db.Customers.AnyAsync())
        {
            db.Customers.AddRange(
                new Customer { Code = "SELF", Name = "本公司(制药厂)", Province = "北京市", Phone = "400-8888888", CustomerType = CustomerType.商业 },
                new Customer { Code = "D1001", Name = "华东医药股份有限公司", Province = "上海市", SalesPerson = "张经理", Phone = "021-33887766", CustomerType = CustomerType.商业, Level = DistributorLevel.一级经销商 },
                new Customer { Code = "D1002", Name = "华南医药批发公司", Province = "广东省", SalesPerson = "李经理", Phone = "020-88776655", CustomerType = CustomerType.商业, Level = DistributorLevel.一级经销商 },
                new Customer { Code = "D1003", Name = "湖南医药集团有限公司", Province = "湖南省", SalesPerson = "王经理", Phone = "0731-88886666", CustomerType = CustomerType.商业, Level = DistributorLevel.一级经销商 },
                new Customer { Code = "D2001", Name = "上海浦西医药公司", Province = "上海市", SalesPerson = "赵经理", Phone = "021-55667788", CustomerType = CustomerType.商业, Level = DistributorLevel.二级经销商 },
                new Customer { Code = "D2002", Name = "深圳福田医药公司", Province = "广东省", SalesPerson = "刘经理", Phone = "0755-88776655", CustomerType = CustomerType.商业, Level = DistributorLevel.二级经销商 },
                new Customer { Code = "D2003", Name = "武汉华中医药公司", Province = "湖北省", SalesPerson = "周经理", Phone = "027-88776655", CustomerType = CustomerType.商业, Level = DistributorLevel.二级经销商 },
                new Customer { Code = "D3001", Name = "广州白云医药公司", Province = "广东省", SalesPerson = "吴经理", Phone = "020-55667788", CustomerType = CustomerType.商业, Level = DistributorLevel.三级经销商 },
                new Customer { Code = "T0001", Name = "上海华山医院", Province = "上海市", SalesPerson = "陈主任", Phone = "021-52889999", CustomerType = CustomerType.医院 },
                new Customer { Code = "T0002", Name = "上海老百姓大药房", Province = "上海市", SalesPerson = "孙店长", Phone = "021-33887799", CustomerType = CustomerType.药店 },
                new Customer { Code = "T0003", Name = "广东省人民医院", Province = "广东省", SalesPerson = "林主任", Phone = "020-83827812", CustomerType = CustomerType.医院 },
                new Customer { Code = "T0004", Name = "广州仁心堂大药房", Province = "广东省", SalesPerson = "王店长", Phone = "020-88776655", CustomerType = CustomerType.药店 },
                new Customer { Code = "T0005", Name = "北京协和医院", Province = "北京市", SalesPerson = "刘主任", Phone = "010-69156114", CustomerType = CustomerType.医院 },
                new Customer { Code = "T0006", Name = "北京惠民社区诊所", Province = "北京市", SalesPerson = "周医生", Phone = "010-66778899", CustomerType = CustomerType.诊所 },
                new Customer { Code = "T0007", Name = "武汉同济医院", Province = "湖北省", SalesPerson = "张主任", Phone = "027-83663292", CustomerType = CustomerType.医院 }
            );
            await db.SaveChangesAsync();
        }

        // Suppliers
        if (!await db.Suppliers.AnyAsync())
        {
            db.Suppliers.AddRange(
                new Supplier { Code = "SUP01", Name = "华北制药原料供应公司", ContactPerson = "赵总", Phone = "0311-88990011" },
                new Supplier { Code = "SUP02", Name = "广州白云山原料供应公司", ContactPerson = "钱总", Phone = "020-33889900" },
                new Supplier { Code = "SUP03", Name = "江苏豪森原料供应公司", ContactPerson = "孙总", Phone = "0518-88009900" }
            );
            await db.SaveChangesAsync();
        }

        // Multi-level distribution with normal and conflict scenarios
        if (!await db.DistributionRecords.AnyAsync() && await db.Drugs.AnyAsync())
        {
            var drugs = await db.Drugs.Take(3).ToListAsync();
            var c = await db.Customers.ToListAsync();
            var m = c.ToDictionary(x => x.Code);
            var self = m["SELF"]; var d1s = m["D1001"]; var d1g = m["D1002"]; var d1h = m["D1003"];
            var d2s = m["D2001"]; var d2g = m["D2002"]; var d2h = m["D2003"]; var d3g = m["D3001"];
            var t1 = m["T0001"]; var t2 = m["T0002"]; var t3 = m["T0003"]; var t4 = m["T0004"];
            var t5 = m["T0005"]; var t6 = m["T0006"]; var t7 = m["T0007"];

            // 场景1: 正常 厂家→一级(上海)→二级(上海)→终端(上海)
            var b1 = "BATCH0001";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b1, FromCustomerId = self.Id, ToCustomerId = d1s.Id, Quantity = 2000, DistributionDate = DateTime.Now.AddDays(-30) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b1, FromCustomerId = d1s.Id, ToCustomerId = d2s.Id, Quantity = 800, DistributionDate = DateTime.Now.AddDays(-20) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b1, FromCustomerId = d2s.Id, ToCustomerId = t1.Id, Quantity = 300, DistributionDate = DateTime.Now.AddDays(-10) });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b1, Quantity = 5000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b1, Quantity = 1200, CustomerId = d1s.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b1, Quantity = 500, CustomerId = d2s.Id });

            // 场景2: 正常 厂家→一级(广东)→二级(广东)→三级(广东)→终端(广东)
            var b2 = "BATCH0002";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b2, FromCustomerId = self.Id, ToCustomerId = d1g.Id, Quantity = 3000, DistributionDate = DateTime.Now.AddDays(-25) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b2, FromCustomerId = d1g.Id, ToCustomerId = d2g.Id, Quantity = 1500, DistributionDate = DateTime.Now.AddDays(-18) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b2, FromCustomerId = d2g.Id, ToCustomerId = d3g.Id, Quantity = 600, DistributionDate = DateTime.Now.AddDays(-10) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b2, FromCustomerId = d3g.Id, ToCustomerId = t3.Id, Quantity = 200, DistributionDate = DateTime.Now.AddDays(-5) });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b2, Quantity = 6000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b2, Quantity = 1500, CustomerId = d1g.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b2, Quantity = 900, CustomerId = d2g.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b2, Quantity = 400, CustomerId = d3g.Id });

            // 场景3: 串货 一级(上海)→二级(上海)→终端(北京) ⚠️
            var b3 = "BATCH0003";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b3, FromCustomerId = self.Id, ToCustomerId = d1s.Id, Quantity = 1500, DistributionDate = DateTime.Now.AddDays(-28) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b3, FromCustomerId = d1s.Id, ToCustomerId = d2s.Id, Quantity = 500, DistributionDate = DateTime.Now.AddDays(-15) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b3, FromCustomerId = d2s.Id, ToCustomerId = t5.Id, Quantity = 200, DistributionDate = DateTime.Now.AddDays(-7) });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b3, Quantity = 3000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b3, Quantity = 1000, CustomerId = d1s.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b3, Quantity = 300, CustomerId = d2s.Id });

            // 场景4: 串货 一级(广东)→终端(北京) ⚠️
            var b4 = "BATCH0004";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b4, FromCustomerId = self.Id, ToCustomerId = d1g.Id, Quantity = 2000, DistributionDate = DateTime.Now.AddDays(-22) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b4, FromCustomerId = d1g.Id, ToCustomerId = t6.Id, Quantity = 300, DistributionDate = DateTime.Now.AddDays(-8) });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b4, Quantity = 4000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b4, Quantity = 1700, CustomerId = d1g.Id });

            // 场景5: 串货 一级(湖南)→二级(湖北)→终端(湖北) ⚠️
            var b5 = "BATCH0005";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b5, FromCustomerId = self.Id, ToCustomerId = d1h.Id, Quantity = 2500, DistributionDate = DateTime.Now.AddDays(-20) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b5, FromCustomerId = d1h.Id, ToCustomerId = d2h.Id, Quantity = 800, DistributionDate = DateTime.Now.AddDays(-12) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b5, FromCustomerId = d2h.Id, ToCustomerId = t7.Id, Quantity = 300, DistributionDate = DateTime.Now.AddDays(-4) });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b5, Quantity = 5000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b5, Quantity = 1700, CustomerId = d1h.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b5, Quantity = 500, CustomerId = d2h.Id });

            // 场景6: 正常 一级(广东)→终端(广东)
            var b6 = "BATCH0006";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b6, FromCustomerId = self.Id, ToCustomerId = d1g.Id, Quantity = 1000, DistributionDate = DateTime.Now.AddDays(-15) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b6, FromCustomerId = d1g.Id, ToCustomerId = t4.Id, Quantity = 400, DistributionDate = DateTime.Now.AddDays(-6) });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b6, Quantity = 2000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b6, Quantity = 600, CustomerId = d1g.Id });

            // ===================== 补充场景 =====================

            // 场景7: 厂家→一级（上海）纯一级调拨，无下游
            var b7 = "BATCH0007";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b7, FromCustomerId = self.Id, ToCustomerId = d1s.Id, Quantity = 3000, DistributionDate = DateTime.Now.AddDays(-35) });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b7, Quantity = 5000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b7, Quantity = 3000, CustomerId = d1s.Id });

            // 场景8: 正常 厂家→一级（广东）→二级（广东）无终端
            var b8 = "BATCH0008";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b8, FromCustomerId = self.Id, ToCustomerId = d1g.Id, Quantity = 2000, DistributionDate = DateTime.Now.AddDays(-20) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b8, FromCustomerId = d1g.Id, ToCustomerId = d2g.Id, Quantity = 1000, DistributionDate = DateTime.Now.AddDays(-12) });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b8, Quantity = 4000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b8, Quantity = 1000, CustomerId = d1g.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b8, Quantity = 1000, CustomerId = d2g.Id });

            // 场景9: 正常 厂家→一级（上海）→二级（上海）→三级（上海）无终端
            var b9 = "BATCH0009";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b9, FromCustomerId = self.Id, ToCustomerId = d1s.Id, Quantity = 4000, DistributionDate = DateTime.Now.AddDays(-40) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b9, FromCustomerId = d1s.Id, ToCustomerId = d2s.Id, Quantity = 2000, DistributionDate = DateTime.Now.AddDays(-30) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b9, FromCustomerId = d2s.Id, ToCustomerId = d3g.Id, Quantity = 800, DistributionDate = DateTime.Now.AddDays(-20) });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b9, Quantity = 8000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b9, Quantity = 2000, CustomerId = d1s.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b9, Quantity = 1200, CustomerId = d2s.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b9, Quantity = 800, CustomerId = d3g.Id });

            // 场景10: 串货 厂家→一级（湖南）→终端（湖北）⚠️ 一级直发跨省
            var b10 = "BATCH0010";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b10, FromCustomerId = self.Id, ToCustomerId = d1h.Id, Quantity = 1500, DistributionDate = DateTime.Now.AddDays(-18) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b10, FromCustomerId = d1h.Id, ToCustomerId = t7.Id, Quantity = 500, DistributionDate = DateTime.Now.AddDays(-8) });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b10, Quantity = 3000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b10, Quantity = 1000, CustomerId = d1h.Id });

            // 场景11: 串货 厂家→一级（广东）→二级（广东）→三级（广东）→终端（湖南）⚠️ 三级后跨省
            var b11 = "BATCH0011";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b11, FromCustomerId = self.Id, ToCustomerId = d1g.Id, Quantity = 5000, DistributionDate = DateTime.Now.AddDays(-45) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b11, FromCustomerId = d1g.Id, ToCustomerId = d2g.Id, Quantity = 2500, DistributionDate = DateTime.Now.AddDays(-35) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b11, FromCustomerId = d2g.Id, ToCustomerId = d3g.Id, Quantity = 1000, DistributionDate = DateTime.Now.AddDays(-25) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b11, FromCustomerId = d3g.Id, ToCustomerId = t1.Id, Quantity = 300, DistributionDate = DateTime.Now.AddDays(-10) });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b11, Quantity = 8000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b11, Quantity = 2500, CustomerId = d1g.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b11, Quantity = 1500, CustomerId = d2g.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b11, Quantity = 700, CustomerId = d3g.Id });

            // 场景12: 串货 厂家→一级（上海）→终端（湖南）⚠️
            var b12 = "BATCH0012";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b12, FromCustomerId = self.Id, ToCustomerId = d1s.Id, Quantity = 1800, DistributionDate = DateTime.Now.AddDays(-14) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b12, FromCustomerId = d1s.Id, ToCustomerId = t3.Id, Quantity = 400, DistributionDate = DateTime.Now.AddDays(-5) });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b12, Quantity = 3000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b12, Quantity = 1400, CustomerId = d1s.Id });

            // ===== 同级调拨场景 =====

            // 场景13: 同级调拨 一级(上海)→一级(广东) 跨区调拨（串货）
            var b13 = "BATCH0013";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b13, FromCustomerId = self.Id, ToCustomerId = d1s.Id, Quantity = 2000, DistributionDate = DateTime.Now.AddDays(-30) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b13, FromCustomerId = d1s.Id, ToCustomerId = d1g.Id, Quantity = 600, DistributionDate = DateTime.Now.AddDays(-15) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[2].Id, BatchNo = b13, FromCustomerId = d1g.Id, ToCustomerId = t3.Id, Quantity = 200, DistributionDate = DateTime.Now.AddDays(-5) });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b13, Quantity = 4000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b13, Quantity = 1400, CustomerId = d1s.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[2].Id, BatchNo = b13, Quantity = 600, CustomerId = d1g.Id });

            // 场景14: 同级调拨 二级(广东)→二级(上海) 再流向终端
            var b14 = "BATCH0014";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b14, FromCustomerId = self.Id, ToCustomerId = d1g.Id, Quantity = 3000, DistributionDate = DateTime.Now.AddDays(-25) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b14, FromCustomerId = d1g.Id, ToCustomerId = d2g.Id, Quantity = 1200, DistributionDate = DateTime.Now.AddDays(-14) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b14, FromCustomerId = d2g.Id, ToCustomerId = d2s.Id, Quantity = 400, DistributionDate = DateTime.Now.AddDays(-7) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b14, FromCustomerId = d2s.Id, ToCustomerId = t2.Id, Quantity = 150, DistributionDate = DateTime.Now.AddDays(-3) });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b14, Quantity = 5000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b14, Quantity = 1800, CustomerId = d1g.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b14, Quantity = 800, CustomerId = d2g.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b14, Quantity = 400, CustomerId = d2s.Id });

            // 场景15: 同级调拨 一级(湖南)→一级(广东)
            var b15 = "BATCH0015";
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b15, FromCustomerId = self.Id, ToCustomerId = d1h.Id, Quantity = 1800, DistributionDate = DateTime.Now.AddDays(-20) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b15, FromCustomerId = d1h.Id, ToCustomerId = d1g.Id, Quantity = 500, DistributionDate = DateTime.Now.AddDays(-10) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b15, FromCustomerId = d1g.Id, ToCustomerId = d2g.Id, Quantity = 200, DistributionDate = DateTime.Now.AddDays(-5) });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b15, Quantity = 3000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b15, Quantity = 1300, CustomerId = d1h.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b15, Quantity = 500, CustomerId = d1g.Id });

            // ==================== 复杂场景：多对多DAG测试 ====================

            // 场景98: 复杂多源DAG — SELF→3个一级商, 一级间调拨, 二级从两个一级进货, 终端从多路径
            var b98 = "BATCH0098";
            // Tier1: SELF → 三个一级商
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = self.Id, ToCustomerId = d1s.Id, Quantity = 3000, DistributionDate = DateTime.Now.AddDays(-60) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = self.Id, ToCustomerId = d1g.Id, Quantity = 2500, DistributionDate = DateTime.Now.AddDays(-58) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = self.Id, ToCustomerId = d1h.Id, Quantity = 2000, DistributionDate = DateTime.Now.AddDays(-55) });
            // 一级商之间调拨: d1h(湖南)→d1s(上海) 调拨300
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d1h.Id, ToCustomerId = d1s.Id, Quantity = 300, DistributionDate = DateTime.Now.AddDays(-45) });
            // Tier2: 各一级商 → 二级商（d2s从两个一级进货）
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d1s.Id, ToCustomerId = d2s.Id, Quantity = 1200, DistributionDate = DateTime.Now.AddDays(-40) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d1g.Id, ToCustomerId = d2s.Id, Quantity = 600, DistributionDate = DateTime.Now.AddDays(-38) });
            // d1s → d2h(湖北) 串货
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d1s.Id, ToCustomerId = d2h.Id, Quantity = 500, DistributionDate = DateTime.Now.AddDays(-36) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d1g.Id, ToCustomerId = d2g.Id, Quantity = 800, DistributionDate = DateTime.Now.AddDays(-34) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d1h.Id, ToCustomerId = d2h.Id, Quantity = 400, DistributionDate = DateTime.Now.AddDays(-32) });
            // 二级间调拨: d2g(广东)→d2s(上海) 调拨200
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d2g.Id, ToCustomerId = d2s.Id, Quantity = 200, DistributionDate = DateTime.Now.AddDays(-25) });
            // Tier3: 二级 → 三级 (d3从两个二级进货)
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d2s.Id, ToCustomerId = d3g.Id, Quantity = 400, DistributionDate = DateTime.Now.AddDays(-20) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d2g.Id, ToCustomerId = d3g.Id, Quantity = 300, DistributionDate = DateTime.Now.AddDays(-18) });
            // 终端: 多路径到达终端
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d2s.Id, ToCustomerId = t1.Id, Quantity = 200, DistributionDate = DateTime.Now.AddDays(-12) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d2s.Id, ToCustomerId = t2.Id, Quantity = 150, DistributionDate = DateTime.Now.AddDays(-10) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d2g.Id, ToCustomerId = t3.Id, Quantity = 180, DistributionDate = DateTime.Now.AddDays(-8) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d2h.Id, ToCustomerId = t7.Id, Quantity = 200, DistributionDate = DateTime.Now.AddDays(-6) });
            // 三级→终端: d3→t4
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d3g.Id, ToCustomerId = t4.Id, Quantity = 250, DistributionDate = DateTime.Now.AddDays(-4) });
            // 同一个终端从两个不同来源进货 (t4从二级d2g和三级d3g都进)
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d2g.Id, ToCustomerId = t4.Id, Quantity = 100, DistributionDate = DateTime.Now.AddDays(-3) });
            // 跨省串货到终端: d2h→t5(北京)
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[0].Id, BatchNo = b98, FromCustomerId = d2h.Id, ToCustomerId = t5.Id, Quantity = 120, DistributionDate = DateTime.Now.AddDays(-2) });
            // 库存
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b98, Quantity = 10000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b98, Quantity = 1800, CustomerId = d1s.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b98, Quantity = 1100, CustomerId = d1g.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b98, Quantity = 1300, CustomerId = d1h.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b98, Quantity = 600, CustomerId = d2s.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b98, Quantity = 400, CustomerId = d2g.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b98, Quantity = 300, CustomerId = d2h.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[0].Id, BatchNo = b98, Quantity = 450, CustomerId = d3g.Id });

            // 场景99: 极限复杂DAG — 全链路+所有模式
            var b99 = "BATCH0099";
            // 采购: 从2个供应商
            // (供应商通过PurchaseOrder创建，这里不再重复; Distribution记录从SELF开始)
            // SELF → 3个一级商
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = self.Id, ToCustomerId = d1s.Id, Quantity = 5000, DistributionDate = DateTime.Now.AddDays(-90) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = self.Id, ToCustomerId = d1g.Id, Quantity = 4000, DistributionDate = DateTime.Now.AddDays(-88) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = self.Id, ToCustomerId = d1h.Id, Quantity = 3500, DistributionDate = DateTime.Now.AddDays(-85) });
            // 一级间双向调拨
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d1s.Id, ToCustomerId = d1g.Id, Quantity = 800, DistributionDate = DateTime.Now.AddDays(-70) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d1h.Id, ToCustomerId = d1s.Id, Quantity = 500, DistributionDate = DateTime.Now.AddDays(-68) });
            // Tier2: 三个一级→三个二级（交叉）
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d1s.Id, ToCustomerId = d2s.Id, Quantity = 2000, DistributionDate = DateTime.Now.AddDays(-60) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d1s.Id, ToCustomerId = d2g.Id, Quantity = 600, DistributionDate = DateTime.Now.AddDays(-58) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d1g.Id, ToCustomerId = d2g.Id, Quantity = 1500, DistributionDate = DateTime.Now.AddDays(-55) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d1g.Id, ToCustomerId = d2h.Id, Quantity = 500, DistributionDate = DateTime.Now.AddDays(-52) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d1h.Id, ToCustomerId = d2h.Id, Quantity = 1200, DistributionDate = DateTime.Now.AddDays(-50) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d1h.Id, ToCustomerId = d2s.Id, Quantity = 400, DistributionDate = DateTime.Now.AddDays(-48) });
            // 二级间调拨
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d2s.Id, ToCustomerId = d2g.Id, Quantity = 300, DistributionDate = DateTime.Now.AddDays(-40) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d2h.Id, ToCustomerId = d2s.Id, Quantity = 250, DistributionDate = DateTime.Now.AddDays(-38) });
            // Tier3: 二级→三级
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d2s.Id, ToCustomerId = d3g.Id, Quantity = 800, DistributionDate = DateTime.Now.AddDays(-30) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d2g.Id, ToCustomerId = d3g.Id, Quantity = 600, DistributionDate = DateTime.Now.AddDays(-28) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d2h.Id, ToCustomerId = d3g.Id, Quantity = 350, DistributionDate = DateTime.Now.AddDays(-25) });
            // 终端: 从各层级直接和间接到达
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d1s.Id, ToCustomerId = t1.Id, Quantity = 500, DistributionDate = DateTime.Now.AddDays(-20) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d2s.Id, ToCustomerId = t1.Id, Quantity = 300, DistributionDate = DateTime.Now.AddDays(-18) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d2s.Id, ToCustomerId = t2.Id, Quantity = 400, DistributionDate = DateTime.Now.AddDays(-15) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d2g.Id, ToCustomerId = t3.Id, Quantity = 350, DistributionDate = DateTime.Now.AddDays(-12) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d2g.Id, ToCustomerId = t4.Id, Quantity = 250, DistributionDate = DateTime.Now.AddDays(-10) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d2h.Id, ToCustomerId = t7.Id, Quantity = 300, DistributionDate = DateTime.Now.AddDays(-8) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d3g.Id, ToCustomerId = t3.Id, Quantity = 200, DistributionDate = DateTime.Now.AddDays(-5) });
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d3g.Id, ToCustomerId = t4.Id, Quantity = 180, DistributionDate = DateTime.Now.AddDays(-3) });
            // 跨省串货: d2g→t5(北京)
            db.DistributionRecords.Add(new DistributionRecord { DrugId = drugs[1].Id, BatchNo = b99, FromCustomerId = d2g.Id, ToCustomerId = t5.Id, Quantity = 150, DistributionDate = DateTime.Now.AddDays(-2) });
            // 库存
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b99, Quantity = 15000, CustomerId = self.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b99, Quantity = 2200, CustomerId = d1s.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b99, Quantity = 1600, CustomerId = d1g.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b99, Quantity = 1800, CustomerId = d1h.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b99, Quantity = 800, CustomerId = d2s.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b99, Quantity = 650, CustomerId = d2g.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b99, Quantity = 500, CustomerId = d2h.Id });
            db.Inventories.Add(new Inventory { DrugId = drugs[1].Id, BatchNo = b99, Quantity = 300, CustomerId = d3g.Id });

            // ===== 为测试批次创建采购单（供应商→本公司）=====
            var suppliers = await db.Suppliers.ToListAsync();
            if (suppliers.Count > 0)
            {
                var sup1 = suppliers[0]; var sup2 = suppliers[1];
                var batchPurchaseMap = new Dictionary<string, (Supplier supplier, int qty, DateTime date)>
                {
                    ["BATCH0001"] = (sup1, 7000, DateTime.Now.AddDays(-35)),
                    ["BATCH0002"] = (sup2, 9000, DateTime.Now.AddDays(-30)),
                    ["BATCH0003"] = (sup1, 4500, DateTime.Now.AddDays(-33)),
                    ["BATCH0004"] = (sup2, 6000, DateTime.Now.AddDays(-28)),
                    ["BATCH0005"] = (sup1, 7500, DateTime.Now.AddDays(-25)),
                    ["BATCH0006"] = (sup2, 3000, DateTime.Now.AddDays(-20)),
                    ["BATCH0007"] = (sup1, 8000, DateTime.Now.AddDays(-40)),
                    ["BATCH0008"] = (sup2, 6000, DateTime.Now.AddDays(-25)),
                    ["BATCH0009"] = (sup1, 12000, DateTime.Now.AddDays(-45)),
                    ["BATCH0010"] = (sup1, 4500, DateTime.Now.AddDays(-23)),
                    ["BATCH0011"] = (sup2, 12000, DateTime.Now.AddDays(-50)),
                    ["BATCH0012"] = (sup1, 4800, DateTime.Now.AddDays(-19)),
                    ["BATCH0013"] = (sup2, 6000, DateTime.Now.AddDays(-35)),
                    ["BATCH0014"] = (sup1, 8000, DateTime.Now.AddDays(-30)),
                    ["BATCH0015"] = (sup2, 4800, DateTime.Now.AddDays(-25)),
                    ["BATCH0098"] = (sup1, 18000, DateTime.Now.AddDays(-65)),
                    ["BATCH0099"] = (sup2, 25000, DateTime.Now.AddDays(-95)),
                };

                foreach (var kv in batchPurchaseMap)
                {
                    var batch = kv.Key;
                    var (supplier, totalQty, orderDate) = kv.Value;
                    // Find which drug this batch belongs to
                    var batchRecords = db.DistributionRecords.Local
                        .Where(r => r.BatchNo == batch).ToList();
                    var drugId = batchRecords.FirstOrDefault()?.DrugId;
                    if (drugId == null || drugId == Guid.Empty) continue;

                    var po = new PurchaseOrder
                    {
                        OrderNo = $"PO-{batch}",
                        SupplierId = supplier.Id,
                        OrderDate = orderDate,
                        TotalAmount = totalQty * 15m,
                        Status = OrderStatus.已完成,
                        Operator = "系统",
                        AuditTime = orderDate.AddDays(1),
                        Auditor = "管理员",
                    };
                    po.Items.Add(new PurchaseOrderItem
                    {
                        DrugId = drugId.Value,
                        BatchNo = batch,
                        Quantity = totalQty,
                        UnitPrice = 15m,
                        TotalPrice = totalQty * 15m,
                    });
                    db.PurchaseOrders.Add(po);

                    // 创建采购来源流向记录：供应商 → 本公司
                    // 使采购数据在流向查询中能显示来源
                    db.DistributionRecords.Add(new DistributionRecord
                    {
                        DrugId = drugId.Value,
                        BatchNo = batch,
                        SupplierId = supplier.Id,
                        ToCustomerId = self.Id,
                        Quantity = totalQty,
                        TaxedUnitPrice = 15m,
                        DistributionDate = orderDate,
                        Operator = "系统",
                    });
                }
            }
        }

        await db.SaveChangesAsync();
    }
}
