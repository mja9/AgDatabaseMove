namespace AgDatabaseMove.Unit
{
  using System.Collections.Generic;
  using SmoFacade;
  using Xunit;


  public class BackupFileToolsTest
  {

    [Theory]
    [InlineData(@"https://hello/a.bak")]
    [InlineData(@"https://hello/a.full")]
    [InlineData(@"https://storage-account.blob.core.windows.net/container/file.trn")]
    [InlineData(@"https://hello/a.diff")]
    [InlineData(@"https://1/2/3/4/5/a.diff")]
    [InlineData(@"https://storage-account.blob.core.windows.net/container/file.bad")]
    [InlineData(@"https://storage-account.blob.core.windows.net/container/sql/db_name/backup_2020_09_02_170003_697.trn")]
    [InlineData(@"http://a.bak")]
    [InlineData(@"\\UNC\syntax\path\file")]
    [InlineData(@"\\server\path\file.ext")]
    [InlineData(@"\\server\file")]
    [InlineData(@"\\server\file.ext")]
    [InlineData(@"//Unix/syntax/file.ext")]
    public void ValidUrlTests(string url)
    {
      Assert.True(BackupFileTools.IsUrl(url));
    }

    [Theory]
    [InlineData(@"\\C:/")]
    [InlineData(@"\wrongUNC\file.txt")]
    [InlineData(@"/wrongUNC/file.txt")]
    [InlineData(@"https://storage-account.blob.core.windows.net/dir/")]
    [InlineData(@"http://storage-account.blob.core.windows.net/dir/")]
    public void InvalidUrlTests(string url)
    {
      Assert.False(BackupFileTools.IsUrl(url));
    }

    [Theory]
    [InlineData(BackupFileTools.BackupType.Log, "trn")]
    [InlineData(BackupFileTools.BackupType.Diff, "diff")]
    [InlineData(BackupFileTools.BackupType.Full, "bak")]
    public void BackupTypeToExtensionTest(BackupFileTools.BackupType type, string ext)
    {
      Assert.Equal(ext, BackupFileTools.BackupTypeToExtension(type));
    }

    [Theory]
    [InlineData("L", BackupFileTools.BackupType.Log)]
    [InlineData("I", BackupFileTools.BackupType.Diff)]
    [InlineData("D", BackupFileTools.BackupType.Full)]
    public void BackupTypeAbbrevToType(string abbrev, BackupFileTools.BackupType type)
    {
      Assert.Equal(type, BackupFileTools.BackupTypeAbbrevToType(abbrev));
    }

    [Theory]
    [InlineData(@"C:\dir\file.ext")]
    [InlineData(@"C:\dir\")]
    [InlineData(@"C:\dir")]
    [InlineData(@"C:\")]
    [InlineData(@"/some/file")]
    [InlineData(@"/dir")]
    [InlineData(@"/")]
    public void ValidPathTests(string path)
    {
      Assert.True(BackupFileTools.IsValidPath(path));
    }

    [Theory]
    [InlineData(@"")]
    [InlineData(@" ")]
    [InlineData(@"file.ext")]
    [InlineData(@"dir\file.ext")]
    [InlineData(@"C dir\file.ext")]
    [InlineData(@"dir")]
    [InlineData(@"C:\inval|d")]
    public void InValidPathTests(string path)
    {
      Assert.False(BackupFileTools.IsValidPath(path));
    }
  }
}