using Microsoft.AspNetCore.Builder;

namespace TradeImportsGmrProcessor.Test.Config;

public class EnvironmentTest
{

   [Fact]
   public void IsNotDevModeByDefault()
   { 
       var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
       var isDev = TradeImportsGmrProcessor.Config.Environment.IsDevMode(builder);
       Assert.False(isDev);
   }
}
