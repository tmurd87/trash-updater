using System.IO.Abstractions;
using System.IO.Abstractions.Extensions;
using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Recyclarr.TestLibrary;
using TestLibrary.FluentAssertions;
using TrashLib.Services.CustomFormat.Guide;
using TrashLib.TestLibrary;

namespace TrashLib.Tests.CustomFormat.Guide;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class CustomFormatLoaderTest : IntegrationFixture
{
    [Test]
    public void Get_custom_format_json_works()
    {
        var sut = Resolve<ICustomFormatLoader>();
        Fs.AddFile("first.json", new MockFileData("{'name':'first','trash_id':'1'}"));
        Fs.AddFile("second.json", new MockFileData("{'name':'second','trash_id':'2'}"));
        Fs.AddFile("collection_of_cfs.md", new MockFileData(""));

        var dir = Fs.CurrentDirectory();
        var results = sut.LoadAllCustomFormatsAtPaths(new[] {dir}, dir.File("collection_of_cfs.md"));

        results.Should().BeEquivalentTo(new[]
        {
            NewCf.Data("first", "1") with {FileName = "first.json"},
            NewCf.Data("second", "2") with {FileName = "second.json"}
        });
    }

    [Test]
    public void Trash_properties_are_removed()
    {
        Fs.AddFile("collection_of_cfs.md", new MockFileData(""));
        Fs.AddFile("first.json", new MockFileData(@"
{
  'name':'first',
  'trash_id':'1',
  'trash_foo': 'foo',
  'trash_bar': 'bar',
  'extra': 'e1'
}"));

        var sut = Resolve<ICustomFormatLoader>();

        var dir = Fs.CurrentDirectory();
        var results = sut.LoadAllCustomFormatsAtPaths(
            new[] {dir}, dir.File("collection_of_cfs.md"));

        const string expectedExtraJson = @"{'name':'first','extra': 'e1'}";

        results.Should()
            .ContainSingle().Which.Json.Should()
            .BeEquivalentTo(JObject.Parse(expectedExtraJson), op => op.Using(new JsonEquivalencyStep()));
    }
}
