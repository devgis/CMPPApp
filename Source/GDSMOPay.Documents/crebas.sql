/*==============================================================*/
/* DBMS name:      ORACLE Version 9i2                           */
/* Created on:     2011-5-6 15:57:06                            */
/*==============================================================*/


alter table GDSMPAY.MESSAGE
   drop primary key cascade;

drop table GDSMPAY.MESSAGE cascade constraints;

drop user GDSMPAY;

/*==============================================================*/
/* User: GDSMPAY                                                */
/*==============================================================*/
create user GDSMPAY identified by '';

/*==============================================================*/
/* Table: MESSAGE                                               */
/*==============================================================*/
create table GDSMPAY.MESSAGE  (
   MSGID                NUMBER                          not null,
   PN                   VARCHAR2(255),
   MESSAGE              VARCHAR2(255),
   "RecvDate"           CHAR(10),
   "DoDate"             CHAR(10)
)
pctfree 10
pctused 40
initrans 1
maxtrans 255
storage
(
    initial 64K
    minextents 1
    maxextents unlimited
    freelists 1
    freelist groups 1
)
tablespace SYSTEM
logging
 noparallel;

comment on column GDSMPAY.MESSAGE.MSGID is
'���';

comment on column GDSMPAY.MESSAGE.PN is
'�ֻ���';

comment on column GDSMPAY.MESSAGE.MESSAGE is
'��Ϣ����';

comment on column GDSMPAY.MESSAGE."RecvDate" is
'����ʱ��';

comment on column GDSMPAY.MESSAGE."DoDate" is
'����ʱ��';

alter table GDSMPAY.MESSAGE
   add constraint PK_MESSAGE primary key (MSGID);

