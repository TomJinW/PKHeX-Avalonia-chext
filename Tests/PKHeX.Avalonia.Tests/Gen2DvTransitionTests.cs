using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

public sealed class Gen2DvTransitionTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(201)]
    public async Task EveryShinyDvCanBeTurnedOffAndOnWithoutHanging(int species)
    {
        await Task.Run(() =>
        {
            foreach (int attack in new[] { 2, 3, 6, 7, 10, 11, 14, 15 })
            {
                var pk = new PK2 { Species = (ushort)species, CurrentLevel = 20, IV_ATK = attack, IV_DEF = 10, IV_SPA = 10, IV_SPE = 10 };
                var vm = TestHelpers.CreateTestViewModel(pk, new SAV2()).VM;
                Assert.True(vm.IsShiny);
                var form = vm.Form;
                var gender = vm.Gender;
                vm.ToggleShinyCommand.Execute(null);
                Assert.False(vm.IsShiny);
                Assert.False(vm.PreparePKM().IsShiny);
                Assert.Equal(form, vm.Form);
                Assert.Equal(gender, vm.Gender);
                Assert.Equal(attack, vm.IvATK);
                Assert.Equal(11, vm.IvDEF);
                var dv = ((PK2)vm.PreparePKM()).DV16;
                vm.RefreshSprite();
                Assert.Equal(dv, ((PK2)vm.PreparePKM()).DV16);
                vm.ToggleShinyCommand.Execute(null);
                Assert.True(vm.IsShiny);
                Assert.True(vm.PreparePKM().IsShiny);
            }
        }).WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task AllUnownLettersResolveAndImpossibleFormsAreRejected()
    {
        await Task.Run(() =>
        {
            for (int form = 0; form < 26; form++)
            {
                var pk = new PK2 { Species = 201, CurrentLevel = 20, IV_ATK = 2, IV_DEF = 10, IV_SPA = 10, IV_SPE = 10 };
                var vm = TestHelpers.CreateTestViewModel(pk, new SAV2()).VM;
                vm.Form = form;
                var prepared = (PK2)vm.PreparePKM();
                Assert.Equal(form, prepared.Form);
                Assert.Equal(form is 8 or 21, prepared.IsShiny);
                Assert.Equal(prepared.IV_HP, vm.IvHP);
                Assert.Equal(prepared.IV_SPA, vm.IvSPD);
                var dv = prepared.DV16;
                vm.RefreshSprite();
                Assert.Equal(dv, ((PK2)vm.PreparePKM()).DV16);
                foreach (var invalid in new[] { -1, 26, 27, 255, 256 })
                {
                    vm.Form = invalid;
                    Assert.Equal(form, vm.Form);
                    Assert.Equal(dv, ((PK2)vm.PreparePKM()).DV16);
                }
                vm.IsShiny = true;
                Assert.True(vm.PreparePKM().IsShiny);
                Assert.Contains(vm.Form, new[] { 8, 21 });
                vm.IsShiny = false;
                Assert.False(vm.PreparePKM().IsShiny);
            }
        }).WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Gen1ShinyDvsAlsoUseDvChanges()
    {
        var pk = new PK1 { Species = 25, CurrentLevel = 20, IV_ATK = 2, IV_DEF = 10, IV_SPA = 10, IV_SPE = 10 };
        var vm = TestHelpers.CreateTestViewModel(pk, new SAV1()).VM;
        vm.IsShiny = false;
        Assert.False(vm.PreparePKM().IsShiny);
        vm.ToggleShinyCommand.Execute(null);
        var shiny = (PK1)vm.PreparePKM();
        Assert.True(shiny.IsShiny);
        var save = new SAV1();
        save.SetPartySlotAtIndex(shiny, 0);
        var reloaded = new SAV1(save.Write().ToArray(), LanguageID.English, save.Version);
        var restored = (PK1)reloaded.GetPartySlotAtIndex(0);
        Assert.Equal(shiny.DV16, restored.DV16);
        Assert.True(restored.ConvertToPK2().IsShiny);
    }
}
