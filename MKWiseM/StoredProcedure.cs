using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKWiseM
{
    internal static class StoredProcedure
    {
        public static string GetScanDensity()
        {
            return @"
            CREATE OR ALTER PROCEDURE dbo.GetScanDensity
                @TableNames NVARCHAR(MAX), -- ex) Table1, Table2, Table3
                @ReturnMessage NVARCHAR(MAX) = '' OUTPUT -- 조회실패 테이블명 리턴
            AS
            BEGIN
                SET NOCOUNT ON;
                SET @ReturnMessage = N'조회 실패 테이블명 -> ';

                --DBCC SHOWCONTIG 결과 임시
                DECLARE @SCAN_RESULTS TABLE
                            (
                                ObjectName SYSNAME, ObjectId INT, IndexName SYSNAME, IndexId INT, Level INT,
                                Pages INT, Rows INT, MinimumRecordSize INT, MaximumRecordSize INT, AverageRecordSize INT,
                                ForwardedRecords INT, Extents INT, ExtentSwitches INT, AverageFreeBytes INT, AveragePageDensity REAL,
                                ScanDensity REAL, BestCount INT, ActualCount INT, LogicalFragmentation REAL,ExtentFragmentation REAL
                            );

                --RETURN TABLE
                DECLARE @ReturnTable TABLE
                            (
                                TableName VARCHAR(128), Rows INT, ScanDensity Numeric(5, 2), ReIdxTime NUMERIC(10, 2) default null
                            );

                --@TableNames -> XML -> @TableList [2008버전에선 STRING_SPLIT 사용불가]
                DECLARE @TableList TABLE
                           (
                               TableName NVARCHAR(128)
                           );
                DECLARE @XML XML;
                SET @XML = CAST('<tables><table>' + REPLACE(@TableNames, ',', '</table><table>') + '</table></tables>' as XML)

                INSERT INTO @TableList
                SELECT tables.tableNode.value('.', 'VARCHAR(MAX)')
                FROM @XML.nodes('/tables/table') tables(tableNode)

                --Cursor Loop [START]
                DECLARE @SQL NVARCHAR(MAX);
                DECLARE @CurrentTableName NVARCHAR(MAX);
                DECLARE TableCursor CURSOR FOR
                    SELECT LTRIM(RTRIM(TableName)) From @TableList; -- 루프 테이블명 TRIM

                --Reindexing Elapsed Time
                DECLARE @StartTime TIME;
                DECLARE @EndTime TIME;
                DECLARE @ElapsedTime NUMERIC(10, 2); --소요시간 ###.##초

                OPEN TableCursor;
                RAISERROR(N'-- ReIdx 작업 시작 --', 0, 1) WITH NOWAIT;
                BEGIN TRY
                    BEGIN TRANSACTION
                    FETCH NEXT FROM TableCursor INTO @CurrentTableName;
                    WHILE @@FETCH_STATUS = 0
                        BEGIN
                            --존재하지 않는 테이블인 경우
                            IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @CurrentTableName)
                                BEGIN
                                    RAISERROR(N'%s 테이블 없음. 다음 테이블 진행', 0, 1, @CurrentTableName) WITH NOWAIT
                                    SET @ReturnMessage = @ReturnMessage + @CurrentTableName + ',';
                                    FETCH NEXT FROM TableCursor INTO @CurrentTableName;
                                    CONTINUE;
                                END

                            BEGIN
                                RAISERROR(N'[ %s ]  ::: 작업 시작 ', 0, 1, @CurrentTableName) WITH NOWAIT
                                SET @SQL = N'DBCC SHOWCONTIG(' + @CurrentTableName + ') with FAST, TABLERESULTS, NO_INFOMSGS';

                                INSERT INTO @SCAN_RESULTS EXEC sp_executesql @sql; -- SHOWCONTIG -> 임시테이블

                                INSERT INTO @ReturnTable (TableName, Rows, ScanDensity)
                                SELECT DISTINCT A.ObjectName, B.rows, A.ScanDensity
                                FROM @SCAN_RESULTS as A
                                         INNER JOIN (SELECT rows, id
                                                     FROM sys.sysindexes
                                                     WHERE id = object_id(@CurrentTableName)
                                                       and indid < 2) as B -- COUNT(*)와 동일
                                                    ON A.objectId = B.Id
                                WHERE A.ObjectName = @CurrentTableName;


                                -- 95%미만 리인덱싱 후 시간기록
                                IF EXISTS(SELECT 1 FROM @ReturnTable WHERE ScanDensity < 95 and TableName = @CurrentTableName)
                                    BEGIN
                                        RAISERROR('[ %s ] ::: RE-INDEXING', 0, 1, @CurrentTableName) WITH NOWAIT;
                                        SET @StartTime = getDate();
                                        SET @SQL = 'ALTER INDEX ALL ON ' + @CurrentTableName + ' REORGANIZE';
                                        EXEC SP_executesql @SQL;
                                        SET @EndTime = getDate();

                                        SET @ElapsedTime = DATEDIFF(MilliSecond, @StartTime, @EndTime) / 1000.0
                                        UPDATE @ReturnTable SET ReIdxTime = @ElapsedTime WHERE TableName = @CurrentTableName;
                                    END
                                RAISERROR(N'[ %s ] ::: 작업 종료 ', 0, 1, @CurrentTableName) WITH NOWAIT;
                                FETCH NEXT FROM TableCursor INTO @CurrentTableName;
                            END
                        END

                CLOSE TableCursor;
                DEALLOCATE TableCursor;

                --RETURN TABLE
                SELECT 
                       TableName as '테이블',
                       Rows as '행 개수',
                       ScanDensity as '데이터 밀도 (%)',
                       ReidxTime as '재구성 소요시간'
                FROM @ReturnTable;

                --RETURN 조회실패 테이블명 @ReturnMessage
                PRINT N'-- ReIdx 작업 종료 --';
            COMMIT TRANSACTION
            END TRY
            BEGIN CATCH
                ROLLBACK TRANSACTION
                PRINT ERROR_MESSAGE()
            END CATCH
            END
            ";
        }
    }
}
