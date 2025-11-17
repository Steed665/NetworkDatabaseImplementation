
-- Locations
CREATE TABLE Location (
    LocationID          INTEGER PRIMARY KEY,
    LocationDescription VARCHAR(100)  
);
-- Core user data
CREATE TABLE CompUser (
    UserID      VARCHAR(50) PRIMARY KEY,
    Surname     VARCHAR(100),            
    Title       VARCHAR(100),             
    LocationID  INTEGER,                 
    FOREIGN KEY (LocationID) REFERENCES Location(LocationID)
);

-- Login IDs
CREATE TABLE LoginAccount (
    LoginID     VARCHAR(50) PRIMARY KEY 
);

-- Link between users and login IDs
CREATE TABLE UserLogin (
    UserID      VARCHAR(50) NOT NULL,
    LoginID     VARCHAR(50) NOT NULL,
    PRIMARY KEY (UserID, LoginID),
    FOREIGN KEY (UserID) REFERENCES CompUser(UserID),
    FOREIGN KEY (LoginID) REFERENCES LoginAccount(LoginID)
);

-- Multiple forenames per user
CREATE TABLE UserForename (
    UserID          VARCHAR(50) NOT NULL,
    ForenameOrder   INTEGER     NOT NULL,
    Forename        VARCHAR(100) NOT NULL,
    PRIMARY KEY (UserID, ForenameOrder),
    FOREIGN KEY (UserID) REFERENCES CompUser(UserID)
);

-- Positions
CREATE TABLE Position (
    PositionID   INTEGER PRIMARY KEY,
    PositionName VARCHAR(200) NOT NULL
);

-- Link between users and positions
CREATE TABLE UserPosition (
    UserID      VARCHAR(50) NOT NULL,
    PositionID  INTEGER     NOT NULL,
    PRIMARY KEY (UserID, PositionID),
    FOREIGN KEY (UserID) REFERENCES CompUser(UserID),
    FOREIGN KEY (PositionID) REFERENCES Position(PositionID)
);

-- Phone numbers
CREATE TABLE Phone (
    PhoneNumber VARCHAR(40) PRIMARY KEY
);

-- Link between users and phones
CREATE TABLE UserPhone (
    UserID      VARCHAR(50) NOT NULL,
    PhoneNumber VARCHAR(40) NOT NULL,
    PRIMARY KEY (UserID, PhoneNumber),
    FOREIGN KEY (UserID) REFERENCES CompUser(UserID),
    FOREIGN KEY (PhoneNumber) REFERENCES Phone(PhoneNumber)
);

-- Email addresses
CREATE TABLE Email (
    EmailAddress VARCHAR(255) PRIMARY KEY
);

-- Link between users and email addresses
CREATE TABLE UserEmail (
    UserID       VARCHAR(50) NOT NULL,
    EmailAddress VARCHAR(255) NOT NULL,
    PRIMARY KEY (UserID, EmailAddress),
    FOREIGN KEY (UserID) REFERENCES CompUser(UserID),
    FOREIGN KEY (EmailAddress) REFERENCES Email(EmailAddress)
);

