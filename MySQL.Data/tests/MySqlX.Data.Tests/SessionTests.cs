// Copyright (c) 2015, 2018, Oracle and/or its affiliates. All rights reserved.
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License, version 2.0, as
// published by the Free Software Foundation.
//
// This program is also distributed with certain software (including
// but not limited to OpenSSL) that is licensed under separate terms,
// as designated in a particular file or component or in included license
// documentation.  The authors of MySQL hereby grant you an
// additional permission to link the program and your derivative works
// with the separately licensed software that they have included with
// MySQL.
//
// Without limiting anything contained in the foregoing, this file,
// which is part of MySQL Connector/NET, is also subject to the
// Universal FOSS Exception, version 1.0, a copy of which can be found at
// http://oss.oracle.com/licenses/universal-foss-exception.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License, version 2.0, for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin St, Fifth Floor, Boston, MA 02110-1301  USA

using MySql.Data.MySqlClient;
using MySqlX.XDevAPI;
using System;
using System.IO;
using Xunit;

namespace MySqlX.Data.Tests
{
  public class SessionTests : BaseTest
  {
    [Fact]
    public void CanCloseSession()
    {
      Session s = MySqlX.XDevAPI.MySQLX.GetSession(ConnectionString);
      Assert.True(s.InternalSession.SessionState == SessionState.Open);
      s.Close();
      Assert.Equal(s.InternalSession.SessionState, SessionState.Closed);
    }

    [Fact]
    public void NoPassword()
    {
      Session session = MySqlX.XDevAPI.MySQLX.GetSession(ConnectionStringNoPassword);
      Assert.True(session.InternalSession.SessionState == SessionState.Open);
      session.Close();
      Assert.Equal(session.InternalSession.SessionState, SessionState.Closed);
    }

    [Fact]
    public void SessionClose()
    {
      Session session = MySQLX.GetSession(ConnectionString);
      Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
      session.Close();
      Assert.Equal(SessionState.Closed, session.InternalSession.SessionState);
    }

    [Fact]
    public void CountClosedSession()
    {
      Session nodeSession = MySQLX.GetSession(ConnectionString);
      int sessions = nodeSession.SQL("show processlist").Execute().FetchAll().Count;

      for (int i = 0; i < 20; i++)
      {
        Session session = MySQLX.GetSession(ConnectionString);
        Assert.True(session.InternalSession.SessionState == SessionState.Open);
        session.Close();
        Assert.Equal(session.InternalSession.SessionState, SessionState.Closed);
      }

      int newSessions = nodeSession.SQL("show processlist").Execute().FetchAll().Count;
      nodeSession.Close();
      Assert.Equal(sessions, newSessions);
    }

    [Fact]
    public void ConnectionStringAsAnonymousType()
    {
      var connstring = new
      {
        server = session.Settings.Server,
        port = session.Settings.Port,
        user = session.Settings.UserID,
        password = session.Settings.Password
      };

      using (var testSession = MySQLX.GetSession(connstring))
      {
        Assert.Equal(SessionState.Open, testSession.InternalSession.SessionState);
      }
    }

    [Fact]
    public void SessionGetSetCurrentSchema()
    {
      using (Session testSession = MySQLX.GetSession(ConnectionString))
      {
        Assert.Equal(SessionState.Open, testSession.InternalSession.SessionState);
        Assert.Null(testSession.GetCurrentSchema());
        Assert.Throws<MySqlException>(() => testSession.SetCurrentSchema(""));
        testSession.SetCurrentSchema(schemaName);
        Assert.Equal(schemaName, testSession.Schema.Name);
        Assert.Equal(schemaName, testSession.GetCurrentSchema().Name);
      }
    }

    [Fact]
    public void SessionUsingSchema()
    {
      using (Session mySession = MySQLX.GetSession(ConnectionString + $";database={schemaName};"))
      {
        Assert.Equal(SessionState.Open, mySession.InternalSession.SessionState);
        Assert.Equal(schemaName, mySession.Schema.Name);
        Assert.Equal(schemaName, mySession.GetCurrentSchema().Name);
        Assert.True(mySession.Schema.ExistsInDatabase());
      }
    }

    [Fact]
    public void SessionUsingDefaultSchema()
    {
      using (Session mySession = MySQLX.GetSession(ConnectionString + $";database={schemaName};"))
      {
        Assert.Equal(SessionState.Open, mySession.InternalSession.SessionState);
        Assert.Equal(schemaName, mySession.DefaultSchema.Name);
        Assert.Equal(schemaName, mySession.GetCurrentSchema().Name);
        Assert.True(mySession.Schema.ExistsInDatabase());
        mySession.SetCurrentSchema("mysql");
        Assert.NotEqual(mySession.DefaultSchema.Name, mySession.Schema.Name);
      }

      // DefaultSchema is null because no database was provided in the connection string/URI.
      using (Session mySession = MySQLX.GetSession(ConnectionString))
      {
        Assert.Equal(SessionState.Open, mySession.InternalSession.SessionState);
        Assert.Equal(null, mySession.DefaultSchema);
      }
    }

    [Fact]
    public void SessionUsingDefaultSchemaWithAnonymousObject()
    {
      var globalSession = GetSession();

      using (var internalSession = MySQLX.GetSession(new
      {
        server = globalSession.Settings.Server,
        port = globalSession.Settings.Port,
        user = globalSession.Settings.UserID,
        password = globalSession.Settings.Password,
        sslmode = MySqlSslMode.Required,
        database = "mysql"
      }))
      {
        Assert.Equal("mysql", internalSession.DefaultSchema.Name);
      }

      // DefaultSchema is null when no database is provided.
      using (var internalSession = MySQLX.GetSession(new
      {
        server = globalSession.Settings.Server,
        port = globalSession.Settings.Port,
        user = globalSession.Settings.UserID,
        password = globalSession.Settings.Password,
        sslmode = MySqlSslMode.Required,
      }))
      {
        Assert.Null(internalSession.DefaultSchema);
      }

      // Access denied error is raised when database does not exist.
      var exception = Assert.Throws<MySqlException>(() => MySQLX.GetSession(new
        {
          server = globalSession.Settings.Server,
          port = globalSession.Settings.Port,
          user = globalSession.Settings.UserID,
          password = globalSession.Settings.Password,
          sslmode = MySqlSslMode.Required,
          database = "test1"
        }
      ));
      Assert.StartsWith("Access denied", exception.Message);
    }

    [Fact]
    public void SessionUsingDefaultSchemaWithConnectionURI()
    {
      using (var session = MySQLX.GetSession(ConnectionStringUri + "?database=mysql"))
      {
        Assert.Equal("mysql", session.DefaultSchema.Name);
      }
    }

    protected void CheckConnectionStringAsUri(string connectionstring, string user, string password, string server, uint port, params string[] parameters)
    {
      string result = this.session.ParseConnectionString(connectionstring);
      var csbuilder = new MySqlXConnectionStringBuilder(result);
      Assert.True(user == csbuilder.UserID, string.Format("Expected:{0} Current:{1} in {2}", user, csbuilder.UserID, connectionstring));
      Assert.True(password == csbuilder.Password, string.Format("Expected:{0} Current:{1} in {2}", password, csbuilder.Password, connectionstring));
      Assert.True(server == csbuilder.Server, string.Format("Expected:{0} Current:{1} in {2}", server, csbuilder.Server, connectionstring));
      Assert.True(port == csbuilder.Port, string.Format("Expected:{0} Current:{1} in {2}", port, csbuilder.Port, connectionstring));
      if (parameters != null)
      {
        if (parameters.Length % 2 != 0)
          throw new ArgumentOutOfRangeException();
        for (int i = 0; i < parameters.Length; i += 2)
        {
          Assert.True(csbuilder.ContainsKey(parameters[i]));
          Assert.Equal(parameters[i + 1], csbuilder[parameters[i]].ToString());
        }
      }
    }

    [Fact]
    public void ConnectionStringAsUri()
    {
      CheckConnectionStringAsUri("mysqlx://myuser:password@localhost:33060", "myuser", "password", "localhost", 33060);
      CheckConnectionStringAsUri("mysqlx://my%3Auser:p%40ssword@localhost:33060", "my:user", "p@ssword", "localhost", 33060);
      CheckConnectionStringAsUri("mysqlx://my%20user:p%40ss%20word@localhost:33060", "my user", "p@ss word", "localhost", 33060);
      CheckConnectionStringAsUri("mysqlx:// myuser : p%40ssword@localhost:33060", "myuser", "p@ssword", "localhost", 33060);
      CheckConnectionStringAsUri("mysqlx://myuser@localhost:33060", "myuser", "", "localhost", 33060);
      CheckConnectionStringAsUri("mysqlx://myuser:p%40ssword@localhost", "myuser", "p@ssword", "localhost", 33060);
      CheckConnectionStringAsUri("mysqlx://myuser:p%40ssw%40rd@localhost", "myuser", "p@ssw@rd", "localhost", 33060);
      CheckConnectionStringAsUri("mysqlx://my%40user:p%40ssword@localhost", "my@user", "p@ssword", "localhost", 33060);
      CheckConnectionStringAsUri("mysqlx://myuser@localhost", "myuser", "", "localhost", 33060);
      CheckConnectionStringAsUri("mysqlx://myuser@127.0.0.1", "myuser", "", "127.0.0.1", 33060);
      CheckConnectionStringAsUri("mysqlx://myuser@[::1]", "myuser", "", "[::1]", 33060);
      CheckConnectionStringAsUri("mysqlx://myuser:password@[2606:b400:440:1040:bd41:e449:45ee:2e1a]", "myuser", "password", "[2606:b400:440:1040:bd41:e449:45ee:2e1a]", 33060);
      CheckConnectionStringAsUri("mysqlx://myuser:password@[2606:b400:440:1040:bd41:e449:45ee:2e1a]:33060", "myuser", "password", "[2606:b400:440:1040:bd41:e449:45ee:2e1a]", 33060);
      Assert.Throws<UriFormatException>(() => CheckConnectionStringAsUri("mysqlx://myuser:password@[2606:b400:440:1040:bd41:e449:45ee:2e1a:33060]", "myuser", "password", "[2606:b400:440:1040:bd41:e449:45ee:2e1a]", 33060));
      Assert.Throws<UriFormatException>(() => CheckConnectionStringAsUri("mysqlx://myuser:password@2606:b400:440:1040:bd41:e449:45ee:2e1a:33060", "myuser", "password", "[2606:b400:440:1040:bd41:e449:45ee:2e1a]", 33060));
      CheckConnectionStringAsUri("mysqlx://myuser:password@[fe80::bd41:e449:45ee:2e1a%17]", "myuser", "password", "[fe80::bd41:e449:45ee:2e1a]", 33060);
      CheckConnectionStringAsUri("mysqlx://myuser:password@[(address=[fe80::bd41:e449:45ee:2e1a%17],priority=100)]", "myuser", "password", "[fe80::bd41:e449:45ee:2e1a]", 33060);
      CheckConnectionStringAsUri("mysqlx://myuser:password@[(address=[fe80::bd41:e449:45ee:2e1a%17]:3305,priority=100)]", "myuser", "password", "[fe80::bd41:e449:45ee:2e1a]", 3305);
      Assert.Throws<UriFormatException>(() => CheckConnectionStringAsUri("mysqlx://myuser:password@[(address=fe80::bd41:e449:45ee:2e1a%17,priority=100)]", "myuser", "password", "[fe80::bd41:e449:45ee:2e1a]", 33060));
      CheckConnectionStringAsUri("mysqlx://myuser@localhost/test", "myuser", "", "localhost", 33060, "database", "test");
      CheckConnectionStringAsUri("mysqlx://myuser@localhost/test?ssl%20mode=none&connectiontimeout=10", "myuser", "", "localhost", 33060, "database", "test", "ssl mode", "None", "connectiontimeout", "10");
      CheckConnectionStringAsUri("mysqlx+ssh://myuser:password@localhost:33060", "myuser", "password", "localhost", 33060);
      CheckConnectionStringAsUri("mysqlx://_%21%22%23%24s%26%2F%3D-%25r@localhost", "_!\"#$s&/=-%r", "", "localhost", 33060);
      CheckConnectionStringAsUri("mysql://myuser@localhost", "", "", "", 33060);
      CheckConnectionStringAsUri("myuser@localhost", "", "", "", 33060);
      Assert.Throws<UriFormatException>(() => CheckConnectionStringAsUri("mysqlx://uid=myuser;server=localhost", "", "", "", 33060));
      CheckConnectionStringAsUri("mysqlx://user:password@server.example.com/", "user", "password", "server.example.com", 33060, "ssl mode", "Required");
      CheckConnectionStringAsUri("mysqlx://user:password@server.example.com/?ssl-ca=(c:%5Cclient.pfx)", "user", "password", "server.example.com", 33060, "ssl mode", "Required", "ssl-ca", "c:\\client.pfx");
      Assert.Throws<NotSupportedException>(() => CheckConnectionStringAsUri("mysqlx://user:password@server.example.com/?ssl-crl=(c:%5Ccrl.pfx)", "user", "password", "server.example.com", 33060, "ssl mode", "Required", "ssl-crl", "(c:\\crl.pfx)"));
    }

    [Fact]
    public void ConnectionUsingUri()
    {
      using (var session = MySQLX.GetSession(ConnectionStringUri))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
      }
    }

    [Fact]
    public void ConnectionStringNull()
    {
      Assert.Throws<ArgumentNullException>(() => MySQLX.GetSession(null));
    }

    [Fact]
    public void SSLSession()
    {
      using (var s3 = MySQLX.GetSession(ConnectionStringUri))
      {
        Assert.Equal(SessionState.Open, s3.InternalSession.SessionState);
        var result = s3.SQL("SHOW SESSION STATUS LIKE 'Mysqlx_ssl_version';").Execute().FetchAll();
        Assert.StartsWith("TLSv1", result[0][1].ToString());
      }
    }

    [Fact]
    public void SSLCertificate()
    {
      string path = "../../../../MySql.Data.Tests/";
      string connstring = ConnectionStringUri + $"/?ssl-ca={path}client.pfx&ssl-ca-pwd=pass";
      using (var s3 = MySQLX.GetSession(connstring))
      {
        Assert.Equal(SessionState.Open, s3.InternalSession.SessionState);
        var result = s3.SQL("SHOW SESSION STATUS LIKE 'Mysqlx_ssl_version';").Execute().FetchAll();
        Assert.StartsWith("TLSv1", result[0][1].ToString());
      }
    }

    [Fact]
    public void SSLEmptyCertificate()
    {
      string connstring = ConnectionStringUri + $"/?ssl-ca=";
      // if certificate is empty, it connects without a certificate
      using (var s1 = MySQLX.GetSession(connstring))
      {
        Assert.Equal(SessionState.Open, s1.InternalSession.SessionState);
        var result = s1.SQL("SHOW SESSION STATUS LIKE 'Mysqlx_ssl_version';").Execute().FetchAll();
        Assert.StartsWith("TLSv1", result[0][1].ToString());
      }
    }

    [Fact]
    public void SSLCrl()
    {
      string connstring = ConnectionStringUri + "/?ssl-crl=crlcert.pfx";
      Assert.Throws<NotSupportedException>(() => MySQLX.GetSession(connstring));
    }

    [Fact]
    public void SSLOptions()
    {
      string connectionString = ConnectionStringUri;
      // sslmode is valid.
      using (var connection = MySQLX.GetSession(connectionString + "?sslmode=required"))
      {
        Assert.Equal(SessionState.Open, connection.InternalSession.SessionState);
      }

      using (var connection = MySQLX.GetSession(connectionString + "?ssl-mode=required"))
      {
        Assert.Equal(SessionState.Open, connection.InternalSession.SessionState);
      }

      // sslenable is invalid.
      Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?sslenable"));
      Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?ssl-enable"));

      // sslmode=Required is default value.
      using (var connection = MySQLX.GetSession(connectionString))
      {
        Assert.Equal(connection.Settings.SslMode, MySqlSslMode.Required);
      }

      // sslmode=Preferred is invalid.
      Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?ssl-mode=Preferred"));

      // sslmode=Required is default value.
      using (var connection = MySQLX.GetSession(connectionString))
      {
        Assert.Equal(MySqlSslMode.Required, connection.Settings.SslMode);
      }

      // sslmode case insensitive.
      using (var connection = MySQLX.GetSession(connectionString + "?SsL-mOdE=required"))
      {
        Assert.Equal(SessionState.Open, connection.InternalSession.SessionState);
      }
      using (var connection = MySQLX.GetSession(connectionString + "?SsL-mOdE=VeRiFyca&ssl-ca=../../../../MySql.Data.Tests/client.pfx&ssl-ca-pwd=pass"))
      {
        Assert.Equal(SessionState.Open, connection.InternalSession.SessionState);
        var uri = connection.Uri;
      }

      // Duplicate SSL connection options send error message.
      ArgumentException ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?sslmode=Required&ssl mode=None"));
      Assert.EndsWith("is duplicated.", ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?ssl-ca-pwd=pass&ssl-ca-pwd=pass"));
      Assert.EndsWith("is duplicated.", ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?certificatepassword=pass&certificatepassword=pass"));
      Assert.EndsWith("is duplicated.", ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?certificatepassword=pass&ssl-ca-pwd=pass"));
      Assert.EndsWith("is duplicated.", ex.Message);

      // send error if sslmode=None and another ssl parameter exists.
      Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?sslmode=None&ssl-ca=../../../../MySql.Data.Tests/certificates/client.pfx").InternalSession.SessionState);
    }

    [Fact]
    public void SSLCertificatePathKeepsCase()
    {
      var certificatePath = "../../../../MySql.Data.Tests/client.pfx";
      // Connection string in basic format.
      string connString = ConnectionString + ";ssl-ca=" + certificatePath + ";ssl-ca-pwd=pass;";
      var stringBuilder = new MySqlXConnectionStringBuilder(connString);
      Assert.Equal(certificatePath, stringBuilder.CertificateFile);
      Assert.Equal(certificatePath, stringBuilder.SslCa);
      Assert.True(stringBuilder.ConnectionString.Contains(certificatePath));
      connString = stringBuilder.ToString();
      Assert.True(connString.Contains(certificatePath));

      // Connection string in uri format.
      string connStringUri = ConnectionStringUri + "/?ssl-ca=" + certificatePath + "& ssl-ca-pwd=pass;";
      using (var session = MySQLX.GetSession(connStringUri))
      {
        Assert.Equal(certificatePath, session.Settings.CertificateFile);
        Assert.Equal(certificatePath, session.Settings.SslCa);
        Assert.True(session.Settings.ConnectionString.Contains(certificatePath));
        connString = session.Settings.ToString();
        Assert.True(connString.Contains(certificatePath));
      }
    }

    // Fix Bug 24510329 - UNABLE TO CONNECT USING TLS/SSL OPTIONS FOR THE MYSQLX URI SCHEME
    [Theory]
    [InlineData("../../../../MySql.Data.Tests/client.pfx")]
    [InlineData("(../../../../MySql.Data.Tests/client.pfx)")]
    [InlineData(@"(..\..\..\..\MySql.Data.Tests\client.pfx")]
    [InlineData("..\\..\\..\\..\\MySql.Data.Tests\\client.pfx")]
    public void SSLCertificatePathVariations(string certificatePath)
    {
      string connStringUri = ConnectionStringUri + "/?ssl-ca=" + certificatePath + "& ssl-ca-pwd=pass;";

      using (var session = MySQLX.GetSession(connStringUri))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
      }
    }

    [Fact]
    public void IPv6()
    {
      var csBuilder = new MySqlXConnectionStringBuilder(ConnectionString);
      csBuilder.Server = "::1";
      csBuilder.Port = uint.Parse(XPort);

      using (var session = MySQLX.GetSession(csBuilder.ToString()))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
      }
    }

    [Fact]
    public void IPv6AsUrl()
    {
      var csBuilder = new MySqlXConnectionStringBuilder(ConnectionString);
      string connString = $"mysqlx://{csBuilder.UserID}:{csBuilder.Password}@[::1]:{XPort}";
      using (Session session = MySQLX.GetSession(connString))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
      }
    }

    [Fact]
    public void IPv6AsAnonymous()
    {
      var csBuilder = new MySqlXConnectionStringBuilder(ConnectionString);
      using (Session session = MySQLX.GetSession(new { server = "::1", user = csBuilder.UserID, password = csBuilder.Password, port = XPort }))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
      }
    }

    [Fact]
    public void MySqlNativePasswordPlugin()
    {
      // TODO: Remove when support for caching_sha2_password plugin is included for X DevAPI.
      if (session.InternalSession.GetServerVersion().isAtLeast(8, 0, 4)) return;

      using (var session = MySQLX.GetSession(ConnectionStringUri))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
        var result = session.SQL("SELECT `User`, `plugin` FROM `mysql`.`user` WHERE `User` = 'test';").Execute().FetchAll();
        Assert.Equal("test", session.Settings.UserID);
        Assert.Equal(session.Settings.UserID, result[0][0].ToString());
        Assert.Equal("mysql_native_password", result[0][1].ToString());
      }
    }

    [Fact]
    public void ConnectUsingSha256PasswordPlugin()
    {
      string userName = "testSha256";
      string password = "mysql";
      string pluginName = "sha256_password";
      string connectionStringUri = ConnectionStringUri.Replace("test:test", string.Format("{0}:{1}", userName, password));

      // User with password over TLS connection.
      using (var session = MySQLX.GetSession(connectionStringUri))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
        var result = session.SQL(string.Format("SELECT `User`, `plugin` FROM `mysql`.`user` WHERE `User` = '{0}';", userName)).Execute().FetchAll();
        Assert.Equal(userName, session.Settings.UserID);
        Assert.Equal(session.Settings.UserID, result[0][0].ToString());
        Assert.Equal(pluginName, result[0][1].ToString());
      }

      // Connect over non-TLS connection.
      using (var session = MySQLX.GetSession(connectionStringUri + "?sslmode=none"))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
        Assert.Equal(MySqlAuthenticationMode.SHA256_MEMORY, session.Settings.Auth);
      }

      // User without password over TLS connection.
      ExecuteSQL(String.Format("ALTER USER {0}@'localhost' IDENTIFIED BY ''", userName));
      using (var session = MySQLX.GetSession(ConnectionStringUri.Replace("test:test", string.Format("{0}:{1}", userName, ""))))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
        var result = session.SQL(string.Format("SELECT `User`, `plugin` FROM `mysql`.`user` WHERE `User` = '{0}';", userName)).Execute().FetchAll();
        Assert.Equal(userName, session.Settings.UserID);
        Assert.Equal(session.Settings.UserID, result[0][0].ToString());
        Assert.Equal(pluginName, result[0][1].ToString());
      }
    }

    [Fact]
    public void ConnectUsingExternalAuth()
    {
      // Should fail since EXTERNAL is currently not supported by X Plugin.
      Exception ex = Assert.Throws<MySqlException>(() => MySQLX.GetSession(ConnectionString + ";auth=EXTERNAL"));
      Assert.Equal("Invalid authentication method EXTERNAL", ex.Message);
    }

    [Fact]
    public void ConnectUsingPlainAuth()
    {
      using (var session = MySQLX.GetSession(ConnectionStringUri + "?auth=pLaIn"))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
        Assert.Equal(MySqlAuthenticationMode.PLAIN, session.Settings.Auth);
      }

      // Should fail since PLAIN requires TLS to be enabled.
      Assert.Throws<MySqlException>(() => MySQLX.GetSession(ConnectionStringUri + "?auth=PLAIN&sslmode=none"));
    }

    [Fact]
    public void ConnectUsingMySQL41Auth()
    {
      var connectionStringUri = ConnectionStringUri;
      if (session.InternalSession.GetServerVersion().isAtLeast(8, 0, 4))
      {
        // Use connection string uri set with a mysql_native_password user.
        connectionStringUri = ConnectionStringUriNative;
      }

      using (var session = MySQLX.GetSession(connectionStringUri + "?auth=MySQL41"))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
        Assert.Equal(MySqlAuthenticationMode.MYSQL41, session.Settings.Auth);
      }

      using (var session = MySQLX.GetSession(connectionStringUri + "?auth=mysql41&sslmode=none"))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
        Assert.Equal(MySqlAuthenticationMode.MYSQL41, session.Settings.Auth);
      }
    }

    [Fact]
    public void DefaultAuth()
    {
      if (!session.InternalSession.GetServerVersion().isAtLeast(8, 0, 5)) return;

      string user = "testsha256";

      session.SQL($"DROP USER IF EXISTS {user}@'localhost'").Execute();
      session.SQL($"CREATE USER {user}@'localhost' IDENTIFIED WITH caching_sha2_password BY '{user}'").Execute();

      string connString = $"mysqlx://{user}:{user}@localhost:{XPort}";
      // Default to PLAIN when TLS is enabled.
      using (var session = MySQLX.GetSession(connString))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
        Assert.Equal(MySqlAuthenticationMode.PLAIN, session.Settings.Auth);
        var result = session.SQL("SHOW SESSION STATUS LIKE 'Mysqlx_ssl_version';").Execute().FetchAll();
        Assert.StartsWith("TLSv1", result[0][1].ToString());
      }

      // Default to SHA256_MEMORY when TLS is not enabled.
      using (var session = MySQLX.GetSession(connString + "?sslmode=none"))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
        Assert.Equal(MySqlAuthenticationMode.SHA256_MEMORY, session.Settings.Auth);
      }
    }

    [Fact]
    public void ConnectUsingSha256Memory()
    {
      if (!session.InternalSession.GetServerVersion().isAtLeast(8, 0, 5)) return;

      using (var session = MySQLX.GetSession(ConnectionStringUri + "?auth=SHA256_MEMORY"))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
        Assert.Equal(MySqlAuthenticationMode.SHA256_MEMORY, session.Settings.Auth);
      }

      using (var session = MySQLX.GetSession(ConnectionStringUri + "?auth=SHA256_MEMORY&sslmode=none"))
      {
        Assert.Equal(SessionState.Open, session.InternalSession.SessionState);
        Assert.Equal(MySqlAuthenticationMode.SHA256_MEMORY, session.Settings.Auth);
      }
    }

    [Fact]
    public void CreateSessionWithUnsupportedOptions()
    {
      var errorMessage = "Option not supported.";
      var connectionUri = string.Format("{0}?", ConnectionStringUri);

      // Use a connection URI.
      var ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "pipe=MYSQL"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "compress=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "allow batch=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "logging=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "sharedmemoryname=MYSQL"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "defaultcommandtimeout=30"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "usedefaultcommandtimeoutforef=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "persistsecurityinfo=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "encrypt=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "integratedsecurity=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "allowpublickeyretrieval=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "autoenlist=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "includesecurityasserts=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "allowzerodatetime=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "convert zero datetime=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "useusageadvisor=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "procedurecachesize=50"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "useperformancemonitor=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "ignoreprepare=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "respectbinaryflags=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "treat tiny as boolean=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "allowuservariables=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "interactive=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "functionsreturnstring=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "useaffectedrows=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "oldguids=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "sqlservermode=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "tablecaching=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "defaulttablecacheage=60"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "checkparameters=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "replication=replication_group"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "exceptioninterceptors=none"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "commandinterceptors=none"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "connectionlifetime=100"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "pooling=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "minpoolsize=0"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "maxpoolsize=20"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "connectionreset=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "cacheserverproperties=true"));
      Assert.StartsWith(errorMessage, ex.Message);

      // Use a connection string.
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession("treatblobsasutf8=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession("blobasutf8includepattern=pattern"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession("blobasutf8excludepattern=pattern"));
      Assert.StartsWith(errorMessage, ex.Message);
    }

    [Fact]
    public void CreateBuilderWithUnsupportedOptions()
    {
      var errorMessage = "Option not supported.";
      var ex = Assert.Throws<ArgumentException>(() => new MySqlXConnectionStringBuilder("pipe=MYSQL"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => new MySqlXConnectionStringBuilder("allow batch=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => new MySqlXConnectionStringBuilder("respectbinaryflags=true"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => new MySqlXConnectionStringBuilder("pooling=false"));
      Assert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => new MySqlXConnectionStringBuilder("cacheserverproperties=true"));
      Assert.StartsWith(errorMessage, ex.Message);
    }

    [Fact]
    public void GetUri()
    {
      using (var internalSession = MySQLX.GetSession(session.Uri))
      {
        // Validate that all properties keep their original value.
        foreach (var connectionOption in session.Settings.values)
        {
          // SslCrl connection option is skipped since it isn't currently supported.
          if (connectionOption.Key == "sslcrl")
            continue;

          try
          {
            Assert.Equal(session.Settings[connectionOption.Key], internalSession.Settings[connectionOption.Key]);
          }
          catch (ArgumentException ex)
          {
            Assert.StartsWith("Option not supported.", ex.Message);
          }
        }
      }
    }

    [Fact]
    public void GetUriWithSSLParameters()
    {
      var session = GetSession();

      MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder();
      builder.Server = session.Settings.Server;
      builder.UserID = session.Settings.UserID;;
      builder.Password = session.Settings.Password;
      builder.Port = session.Settings.Port;
      builder.ConnectionProtocol = MySqlConnectionProtocol.Tcp;
      builder.Database = session.Settings.Database;
      builder.CharacterSet = session.Settings.CharacterSet;
      builder.SslMode = MySqlSslMode.Required;
      builder.SslCa = "../../../../MySql.Data.Tests/client.pfx";
      builder.CertificatePassword = "pass";
      builder.ConnectionTimeout = 10;
      builder.Keepalive = 10;
      builder.Auth = MySqlAuthenticationMode.AUTO;

      var connectionString = builder.ConnectionString;
      string uri = null;

      // Create session with connection string.
      using (var internalSession = MySQLX.GetSession(connectionString))
      {
        uri = internalSession.Uri;
      }

      // Create session with the uri version of the connection string.
      using (var internalSession = MySQLX.GetSession(uri))
      {
        // Compare values of the connection options.
        foreach (var connectionOption in builder.values)
        {
          // SslCrl connection option is skipped since it isn't currently supported.
          if (connectionOption.Key == "sslcrl")
            continue;

          // Authentication mode AUTO/DEFAULT is internally assigned, hence it is expected to be different in this scenario. 
          if (connectionOption.Key == "auth")
            Assert.Equal(MySqlAuthenticationMode.PLAIN, internalSession.Settings[connectionOption.Key]);
          else
            Assert.Equal(builder[connectionOption.Key], internalSession.Settings[connectionOption.Key]);
        }
      }
    }

    [Fact]
    public void GetUriKeepsSSLMode()
    {
      var globalSession = GetSession();
      var builder = new MySqlConnectionStringBuilder();
      builder.Server = globalSession.Settings.Server;
      builder.UserID = globalSession.Settings.UserID;
      builder.Password = globalSession.Settings.Password;
      builder.Port = globalSession.Settings.Port;
      builder.Database = "test";
      builder.CharacterSet = globalSession.Settings.CharacterSet;
      builder.SslMode = MySqlSslMode.VerifyCA;
      // Setting SslCa will also set CertificateFile.
      builder.SslCa = "../../../../MySql.Data.Tests/client.pfx";
      builder.CertificatePassword = "pass";
      builder.ConnectionTimeout = 10;
      builder.Keepalive = 10;
      // Auth will change to the authentication mode internally used PLAIN, MySQL41, SHA256_MEMORY: 
      builder.Auth = MySqlAuthenticationMode.AUTO;
      // Doesn't show in the session.URI because Tcp is the default value. Tcp, Socket and Sockets are treated the same.
      builder.ConnectionProtocol = MySqlConnectionProtocol.Tcp;

      string uri = null;
      using (var internalSession = MySQLX.GetSession(builder.ConnectionString))
      {
        uri = internalSession.Uri;
      }

      using (var internalSession = MySQLX.GetSession(uri))
      {
        Assert.Equal(builder.Server, internalSession.Settings.Server);
        Assert.Equal(builder.UserID, internalSession.Settings.UserID);
        Assert.Equal(builder.Password, internalSession.Settings.Password);
        Assert.Equal(builder.Port, internalSession.Settings.Port);
        Assert.Equal(builder.Database, internalSession.Settings.Database);
        Assert.Equal(builder.CharacterSet, internalSession.Settings.CharacterSet);
        Assert.Equal(builder.SslMode, internalSession.Settings.SslMode);
        Assert.Equal(builder.SslCa, internalSession.Settings.SslCa);
        Assert.Equal(builder.CertificatePassword, internalSession.Settings.CertificatePassword);
        Assert.Equal(builder.ConnectionTimeout, internalSession.Settings.ConnectionTimeout);
        Assert.Equal(builder.Keepalive, internalSession.Settings.Keepalive);
        Assert.Equal(MySqlAuthenticationMode.PLAIN, internalSession.Settings.Auth);
      }
    }
  }
}