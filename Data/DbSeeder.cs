using MenuQr.Models.Mongo;
using MenuQr.Models.Sql;
using MenuQr.Services;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MenuQr.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(MenuDbContext sqlContext, MongoDbService mongoService)
        {
            // 1. Seed SQL Server (Users)
            // Ensure the SQL Server database is created
            await sqlContext.Database.EnsureCreatedAsync();

            if (!await sqlContext.Users.AnyAsync())
            {
                var users = new List<User>
                {
                    new User
                    {
                        Username = "admin",
                        PasswordHash = PasswordHelper.HashPassword("admin123"),
                        FullName = "System Administrator",
                        Role = "Admin",
                        CreatedAt = DateTime.UtcNow
                    },
                    new User
                    {
                        Username = "kitchen",
                        PasswordHash = PasswordHelper.HashPassword("kitchen123"),
                        FullName = "Kitchen Master Chef",
                        Role = "Kitchen",
                        CreatedAt = DateTime.UtcNow
                    },
                    new User
                    {
                        Username = "cashier",
                        PasswordHash = PasswordHelper.HashPassword("cashier123"),
                        FullName = "Cashier Agent",
                        Role = "Cashier",
                        CreatedAt = DateTime.UtcNow
                    }
                };

                await sqlContext.Users.AddRangeAsync(users);
                await sqlContext.SaveChangesAsync();
            }

            // 2. Seed MongoDB (Categories & Dishes)
            var categoryCount = await mongoService.Categories.EstimatedDocumentCountAsync();
            if (categoryCount == 0)
            {
                var appetizersCategory = new Category
                {
                    Name = "Appetizers",
                    Description = "Crispy rolls, light soups, and fresh starters to kick off your meal",
                    ImageUrl = "/images/menu/appetizers.webp",
                    Available = true
                };

                var mainsCategory = new Category
                {
                    Name = "Main Dishes",
                    Description = "Authentic hotpots, hand-pulled noodles, and signature beef platters",
                    ImageUrl = "/images/menu/mains.webp",
                    Available = true
                };

                var drinksCategory = new Category
                {
                    Name = "Drinks",
                    Description = "Refreshing milk teas, herbal teas, fruit juices, and carbonated sodas",
                    ImageUrl = "/images/menu/drinks.webp",
                    Available = true
                };

                var dessertsCategory = new Category
                {
                    Name = "Desserts",
                    Description = "Sweet puddings, fresh seasonal fruit plates, and traditional sweet soup",
                    ImageUrl = "/images/menu/desserts.webp",
                    Available = true
                };

                await mongoService.Categories.InsertManyAsync(new[] { appetizersCategory, mainsCategory, drinksCategory, dessertsCategory });

                // Seed Dishes
                var dishes = new List<Dish>
                {
                    new Dish
                    {
                        CategoryId = appetizersCategory.Id!,
                        Name = "Crispy Fried Spring Rolls",
                        Description = "Traditional vietnamese rolls filled with pork, shrimp, wood-ear mushroom, and glass noodles served with sweet chili sauce",
                        Price = 59000,
                        Image = "/images/menu/spring_rolls.webp",
                        Available = true,
                        Sizes = new List<DishSize>
                        {
                            new DishSize { Name = "Standard", PriceAdjustment = 0 }
                        },
                        Toppings = new List<DishTopping>
                        {
                            new DishTopping { Name = "Extra Sweet Chilli Sauce", Price = 0 },
                            new DishTopping { Name = "Extra Vegetable Wrap", Price = 10000 }
                        }
                    },
                    new Dish
                    {
                        CategoryId = mainsCategory.Id!,
                        Name = "Haidilao Spicy Sichuan Hotpot",
                        Description = "Our world-famous signature spicy hotpot broth with premium beef slices, mixed seafood balls, and assorted fresh vegetables",
                        Price = 499000,
                        Image = "/images/menu/hotpot.webp",
                        Available = true,
                        Sizes = new List<DishSize>
                        {
                            new DishSize { Name = "Standard (2-3 Pax)", PriceAdjustment = 0 },
                            new DishSize { Name = "Large (4-6 Pax)", PriceAdjustment = 150000 }
                        },
                        Toppings = new List<DishTopping>
                        {
                            new DishTopping { Name = "Premium Beef Slices (150g)", Price = 80000 },
                            new DishTopping { Name = "Shrimp Balls (6pcs)", Price = 60000 },
                            new DishTopping { Name = "Enoki Mushrooms", Price = 25000 },
                            new DishTopping { Name = "Fresh Tofu Sheets", Price = 20000 }
                        }
                    },
                    new Dish
                    {
                        CategoryId = mainsCategory.Id!,
                        Name = "Spicy Braised Beef Noodles",
                        Description = "Hand-pulled fresh noodles topped with slow-braised tender beef brisket in a highly fragrant spiced beef broth",
                        Price = 99000,
                        Image = "/images/menu/beef_noodles.webp",
                        Available = true,
                        Sizes = new List<DishSize>
                        {
                            new DishSize { Name = "Regular Size", PriceAdjustment = 0 },
                            new DishSize { Name = "Jumbo Size", PriceAdjustment = 25000 }
                        },
                        Toppings = new List<DishTopping>
                        {
                            new DishTopping { Name = "Marinated Soft-boiled Egg", Price = 15000 },
                            new DishTopping { Name = "Extra Braised Beef (3pcs)", Price = 35000 }
                        }
                    },
                    new Dish
                    {
                        CategoryId = drinksCategory.Id!,
                        Name = "Brown Sugar Boba Milk Tea",
                        Description = "Creamy black milk tea sweetened with slow-cooked brown sugar syrup and warm, chewy tapioca pearls",
                        Price = 49000,
                        Image = "/images/menu/boba_milk_tea.webp",
                        Available = true,
                        Sizes = new List<DishSize>
                        {
                            new DishSize { Name = "Medium (M)", PriceAdjustment = 0 },
                            new DishSize { Name = "Large (L)", PriceAdjustment = 10000 }
                        },
                        Toppings = new List<DishTopping>
                        {
                            new DishTopping { Name = "Extra Brown Sugar Boba", Price = 10000 },
                            new DishTopping { Name = "Cream Cheese Foam", Price = 12000 },
                            new DishTopping { Name = "Egg Pudding", Price = 10000 }
                        }
                    },
                    new Dish
                    {
                        CategoryId = dessertsCategory.Id!,
                        Name = "Mango Pomelo Sago",
                        Description = "Chilled sweet mango puree with sago pearls, fresh mango chunks, and juicy pomelo pulp splits",
                        Price = 65000,
                        Image = "/images/menu/mango_sago.webp",
                        Available = true,
                        Sizes = new List<DishSize>
                        {
                            new DishSize { Name = "Standard", PriceAdjustment = 0 }
                        },
                        Toppings = new List<DishTopping>
                        {
                            new DishTopping { Name = "Extra Mango Scoop", Price = 15000 },
                            new DishTopping { Name = "Coconut Jelly Cube", Price = 8000 }
                        }
                    }
                };

                await mongoService.Dishes.InsertManyAsync(dishes);
            }
        }
    }
}
