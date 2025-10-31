using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKWiseM
{
    internal static class LongQuery
    {
        public static string TableExistsQuery(List<string> tables)
        {
            if (tables.Count == 0) return "('NoTable')";
            string joinedTables = string.Join(", ", tables.Select(table => $"('{table}')"));

            return $@"
                BEGIN
                    DECLARE @TableNames Table (TableName NVARCHAR(255));

                    INSERT INTO @TableNames (TableName)
                    VALUES {joinedTables}


                    SELECT
                        T.TableName     AS TableName,
                        CASE
                            WHEN EXISTS (
                                         SELECT 1 FROM INFORMATION_SCHEMA.TABLES sys
                                         WHERE sys.TABLE_NAME = T.TableName
                                        )
                            THEN '1'
                            ELSE '0'
                        END             AS Result
                    FROM @TableNames T;
                END;
            ";
        }

        public static string ClearLogQuery()
        {
            var catalog = AppConfigUtil.Catalog;
            if (string.IsNullOrEmpty(catalog)) throw new Exception("Catalog not found");

            return $@"
                DECLARE @beforeFileSize NVARCHAR(255) = '';
                DECLARE @afterFileSize NVARCHAR(255) = '';
                RAISERROR(N'로그 축소 시작', 0, 1) WITH NOWAIT

                ALTER DATABASE {catalog}
                SET RECOVERY SIMPLE;

                SELECT @beforeFileSize = concat(size * 8 / 1024, ' MB')
                FROM sys.master_files WHERE type_desc = 'LOG' AND database_id = DB_ID('{catalog}');
                RAISERROR(N'작업 전 LOG파일 사이즈: %s', 0, 1, @beforeFileSize) WITH NOWAIT;

                DBCC SHRINKDATABASE({catalog}, 10, TRUNCATEONLY);

                ALTER DATABASE {catalog}
                SET RECOVERY FULL;

                SELECT @afterFileSize = concat(size * 8 / 1024, ' MB')
                FROM sys.master_files WHERE type_desc = 'LOG' AND database_id = DB_ID('{catalog}');
                RAISERROR(N'작업 후 LOG파일 사이즈: %s', 0, 1, @afterFileSize) WITH NOWAIT;
            ";
        }

        public static string DiskInfoQuery()
        {
            return @"
            BEGIN
               Create table #DiskInfo(
                   Output NVARCHAR(MAX)
               )

               INSERT INTO #DiskInfo EXEC xp_cmdshell 'wmic logicaldisk where ""DriveType=3"" get DeviceID, Size, FreeSpace'

               SELECT * FROM #DiskInfo
               WHERE Output IS NOT NULL AND
                     REPLACE(REPLACE(RTRIM(LTRIM(Output)), CHAR(13), ''), CHAR(10), '') <> '' -- 개행 제거

                DROP TABLE #DiskInfo
            END
            ";
        }

        public static string ColumnFindQuery(string column)
        {
            return $@"
            SELECT
                C.table_schema                             as [Schema],
                C.table_name                               as [TableName],
                STRING_AGG(C.COLUMN_NAME, ', ')            as [ColumnName],
                I.rows                                     as [Rows],
                U.last_user_update                         as [LastAccess]
            FROM INFORMATION_SCHEMA.COLUMNS C
                LEFT JOIN sys.sysindexes I ON object_id(C.Table_Name) = I.id AND i.Indid < 2
                LEFT JOIN sys.dm_db_index_usage_stats U ON object_id(C.Table_Name) = U.object_id AND U.index_id < 2
            WHERE
                C.COLUMN_NAME like '%{column}%'
            GROUP BY
                table_schema, table_name, rows, last_user_update
            ORDER BY
                LastAccess DESC
            ";
        }

        public static string LoadCachedTableQuery()
        {
            return $@"
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

            WITH Buffered AS ( --
                               SELECT
                                 COUNT( * )              AS CachedPageCount
                               , COUNT( * ) * 8.0 / 1024 AS CachedMB
                               , p.object_id
                               FROM sys.dm_os_buffer_descriptors AS bd
                               INNER JOIN sys.allocation_units   AS au ON bd.allocation_unit_id = au.allocation_unit_id
                               INNER JOIN sys.partitions         AS p ON au.container_id = p.hobt_id
                               WHERE bd.database_id = DB_ID( )    -- 현재 데이터베이스만
                                 AND bd.page_type = 'DATA_PAGE'   -- 데이터 페이지만 (인덱스/LOB 포함 시 생략)
                                 AND au.type_desc = 'IN_ROW_DATA' -- 인-로우 데이터
                                 AND OBJECTPROPERTY( p.object_id, 'IsUserTable' ) = 1
                               GROUP BY
                                 bd.database_id
                               , p.object_id
                             )
               , Partition AS (
                               SELECT
                                 ps.object_id
                               , SUM( ps.reserved_page_count )              AS [TotalPageCount]
                               , SUM( ps.reserved_page_count ) * 8.0 / 1024 AS [TotalMB]
                               FROM sys.dm_db_partition_stats AS ps
                               GROUP BY
                                 ps.object_id
                             )
            SELECT
              OBJECT_SCHEMA_NAME( p.object_id, DB_ID( ) )             AS SchemaName
            , OBJECT_NAME( p.object_id, DB_ID( ) )                    AS TableName
            , CONVERT( DECIMAL(10, 2), p.TotalMB )                    AS TotalMB
            , CONVERT( DECIMAL(10, 2), b.CachedMB )                   AS CachedMB
            , CONVERT( DECIMAL(10, 2), b.CachedMB / p.TotalMB * 100 ) AS MB_Percent
            , CONVERT( DECIMAL(10, 2), p.TotalPageCount )             AS TotalPage
            , CONVERT( DECIMAL(10, 2), b.CachedPageCount )            AS CachedPage
            FROM Partition     p
            LEFT JOIN Buffered b ON p.object_id = b.object_id
            WHERE OBJECT_SCHEMA_NAME( p.object_id, DB_ID( ) ) <> 'sys'
            ORDER BY
              CachedMB DESC
            , TotalMB  DESC             
            ";
        }
    }
}
