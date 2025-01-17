using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using MvcMessageLogger.DataAccess;
using System.Net;
using Microsoft.Extensions.Hosting;
using MvcMessageLogger.Models;
using System.Xml.Linq;


namespace MvcMessageLogger.FeatureTests
{
    [Collection("Controller Tests")]
    public class UsersTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public UsersTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        private MvcMessageLoggerContext GetDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<MvcMessageLoggerContext>();
            optionsBuilder.UseInMemoryDatabase("TestDatabase");

            var context = new MvcMessageLoggerContext(optionsBuilder.Options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            return context;
        }

        [Fact]
        public async Task Test_Index_ReturnsViewWithUsers()
        {
            var context = GetDbContext();
            context.Users.Add(new User { Username = "JimComedy123", Name = "Jim" });
            context.Users.Add(new User { Username = "JamesRock98", Name = "James" });
            context.SaveChanges();

            var client = _factory.CreateClient();
            var response = await client.GetAsync("/users");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("JimComedy123", html);
            Assert.Contains("Jim", html);

            Assert.Contains("JamesRock98", html);
            Assert.Contains("James", html);
        }

        [Fact]
        public async Task Test_AddUser_ReturnsRedirectToIndex()
        {
            // Context is only needed if you want to assert against the database
            var context = GetDbContext();

            // Arrange
            var client = _factory.CreateClient();
            var formData = new Dictionary<string, string>
            {
                { "Username", "Joe1011" },
                { "Name", "Joe" },
                { "Password", "Password123" },
                { "CoffeeOfChoice", "VLatte" }
            };

            // Act
            var response = await client.PostAsync("/Users", new FormUrlEncodedContent(formData));
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            Assert.Contains("UserName: Joe1011", html);
            Assert.Contains("Name: Joe", html);
        }

        [Fact]
        public async Task Test_UsersHaveLogInButton()
        {
            var context = GetDbContext();
            context.Users.Add(new User { Username = "JimComedy123", Name = "Jim" });
            context.SaveChanges();

            var client = _factory.CreateClient();
            var response = await client.GetAsync("/users");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("UserName: JimComedy123", html);
            Assert.Contains("Log-In", html);
        }

        [Fact]
        public async Task Test_UsersCanSeeAllMessages()
        {
            var context = GetDbContext();
            var Jim = new User { Username = "JimComedy123", Name = "Jim" };
            Jim.Messages.Add(new Message { Content = "Hello world!", CreatedAt = DateTime.UtcNow});
            Jim.Messages.Add(new Message { Content = "Hello world two!", CreatedAt = DateTime.UtcNow });
            context.Users.Add(Jim);
            context.SaveChanges();

            var client = _factory.CreateClient();
            var response = await client.GetAsync($"/users/{Jim.Id}");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Hello world!", html);
            Assert.Contains("Hello world two!", html);
        }

        [Fact]
        public async Task Test_AUserCanAddAMessage_InShowPage()
        {
            // Context is only needed if you want to assert against the database
            var context = GetDbContext();

            // Arrange
            var client = _factory.CreateClient();
            var Jim = new User { Username = "JimComedy123", Name = "Jim" };
            context.Users.Add(Jim);
            context.SaveChanges();

            var formData = new Dictionary<string, string>
            {
                { "Content", "Hello world!" }
            };

            // Act
            var response = await client.PostAsync($"/Users/{Jim.Id}/messages", new FormUrlEncodedContent(formData));
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.Contains("Hello world!", html);
        }

        [Fact]
        public async Task Test_AUserCanSeeStats_OnStatsPage()
        {
            var context = GetDbContext();

            // Arrange
            var client = _factory.CreateClient();
            var time = DateTime.UtcNow;
            var Jim = new User { Username = "JimComedy123", Name = "Jim" };
            Jim.Messages.Add(new Message { Content = "Hello coffee!", CreatedAt = time });
            Jim.Messages.Add(new Message { Content = "Hello, coffee is good!", CreatedAt = time });
            context.Users.Add(Jim);
            context.SaveChanges();

            var response = await client.GetAsync($"/Users/stats");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Count of Messages: 2", html);
            Assert.Contains("Most Used Words: Hello", html);
            Assert.Contains("How many times \"Coffee\" is mentioned: 2", html);
        }

        [Fact]
        public async Task Test_MostUsedWordsMethod_ReturnsCorrectValue()
        {
            var context = GetDbContext();

            // Arrange
            var client = _factory.CreateClient();
            var time = DateTime.UtcNow;
            var Jim = new User { Username = "JimComedy123", Name = "Jim" };
            Jim.Messages.Add(new Message { Content = "coffee", CreatedAt = time });
            Jim.Messages.Add(new Message { Content = "coffee is good!", CreatedAt = time });
            context.Users.Add(Jim);

            var Joe = new User { Username = "JoeComedy123", Name = "Joe" };
            Joe.Messages.Add(new Message { Content = "Hello", CreatedAt = time });
            Joe.Messages.Add(new Message { Content = "Hello coffee is good!", CreatedAt = time });
            context.Users.Add(Joe);
            context.SaveChanges();

            var response = await client.GetAsync($"/Users/stats");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal("coffee", Jim.MostUsedWord(Jim));
            Assert.Equal("Hello", Joe.MostUsedWord(Joe));
        }

        [Fact]
        public async Task Test_HourWithMostMessages_ReturnsCorrectValue()
        {
            var context = GetDbContext();

            // Arrange
            var client = _factory.CreateClient();
            var time = DateTime.UtcNow;
            var Jim = new User { Username = "JimComedy123", Name = "Jim" };
            Jim.Messages.Add(new Message { Content = "coffee", CreatedAt = time });
            Jim.Messages.Add(new Message { Content = "coffee is good!", CreatedAt = time });
            context.Users.Add(Jim);

            var Joe = new User { Username = "JoeComedy123", Name = "Joe" };
            Joe.Messages.Add(new Message { Content = "Hello", CreatedAt = time });
            Joe.Messages.Add(new Message { Content = "Hello coffee is good!", CreatedAt = time });
            context.Users.Add(Joe);
            context.SaveChanges();

            var response = await client.GetAsync($"/Users/stats");
            var html = await response.Content.ReadAsStringAsync();

            TimeSpan hourAsTimeSpan = TimeSpan.FromHours(time.Hour);

            string formattedStringHour = hourAsTimeSpan.ToString("hh':'mm");

            // Might have to chnage hard coded value later if I run test again and it changes time since I am 
            // calling DateTime.UtcNow;
            Assert.Equal(formattedStringHour, Jim.HourWithMostMessages(Jim));
            Assert.Equal(formattedStringHour, Joe.HourWithMostMessages(Joe));
        }

        [Fact]
        public async Task Test_Delete_RemovesUserFromAllUsers()
        {
            // Arrange
            var context = GetDbContext();
            var client = _factory.CreateClient();
            var time = DateTime.UtcNow;

            var Jim = new User { Username = "JimComedy123", Name = "Jim" };
            Jim.Messages.Add(new Message { Content = "coffee", CreatedAt = time });
            context.Users.Add(Jim);
            context.SaveChanges();

            // Act
            var response = await client.PostAsync($"/users/delete/{Jim.Id}", null);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.DoesNotContain("JimComedy123", html);
        }

        [Fact]
        public async Task Update_SavesChangesToUser()
        {
            // Arrange
            var context = GetDbContext();
            var client = _factory.CreateClient();

            var Jim = new User { Username = "JimComedy123", Name = "Jim" };
            context.Users.Add(Jim);
            context.SaveChanges();

            var formData = new Dictionary<string, string>
            {
                { "Username", "Jim321" },
                { "Name", "Jimmy" },
                { "Password", "Password123" },
                { "CoffeeOfChoice", "VLatte" }
            };

            // Act
            var response = await client.PostAsync(
                $"/users/{Jim.Id}",
                new FormUrlEncodedContent(formData)
            );
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Contains("Jimmy", html);
            Assert.Contains("Jim321", html);
            Assert.DoesNotContain("Comedy", html);
        }
    }
}