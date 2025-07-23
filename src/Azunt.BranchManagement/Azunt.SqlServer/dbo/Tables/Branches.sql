-- [0][0] 브랜치: Branches 
CREATE TABLE [dbo].[Branches] (
    [Id]              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,         -- 자동 증가하는 고유 ID
    [BranchName]      NVARCHAR(100) NULL,                             -- 지점 이름
    [Location]        NVARCHAR(255) NULL,                             -- 지점 위치 (주소 또는 지역)
    [ContactNumber]   NVARCHAR(20) NULL,                              -- 지점 연락처 번호
    [EstablishedDate] DATE NULL,                                      -- 지점 설립일
    [IsActive]        BIT NULL DEFAULT(1)                             -- 지점 활성 상태 (1: Active, 0: Inactive)
);